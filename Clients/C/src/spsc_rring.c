
// See the corresponding header for function-level documentation.

#include <stdio.h>
#include <stdlib.h>
#include <assert.h>
#include "ambrosia/internal/spsc_rring.h"

#if _WIN32
#else
  #include <sched.h> // sched_yield
#endif

// ----------------------------------------------------------------------------
// FIXME: replace globals with a proper API for dynamically allocating buffers:
// ----------------------------------------------------------------------------

// TODO: FACTOR THESE INTO A STRUCT TO ALLOW MORE THAN ONE INSTANCE:

// Single-producer Single-consumer concurrent ring buffer:
char* g_buffer = NULL;
volatile int g_buffer_head = 0; // Byte offset into buffer, written by consumer.
volatile int g_buffer_tail = 0; // Byte offset into buffer, written by producer.
// volatile int g_buffer_msgs = 0; // Count of complete "messages" in g_buffer
volatile int g_buffer_end = -1; // The current capacity, MODIFIED dynamically by PRODUCER.

int orig_buffer_end = -1; // Snapshot of the original buffer capacity.

int g_buffer_last_reserved = -1; // The number of bytes in the last reserve call (producer-private)


// Debugging
//--------------------------------------------------------------------------------

// Fine-grained debugging.  Turned off statically to avoid overhead.
#ifdef SPSC_RRING_DEBUG
volatile int64_t spsc_debug_lock = 0;
void spsc_rring_debug_log(const char *format, ...)
{
    va_list args;
    va_start(args, format);
    sleep_seconds((double)(rand()%1000) * 0.00001); // .01 - 10 ms
    while ( 1 == InterlockedCompareExchange64(&spsc_debug_lock, 1, 0) ) { }
    fprintf(dbg_fd," [AMBCLIENT] ");
    vfprintf(dbg_fd,format, args);
    fflush(dbg_fd);
    spsc_debug_lock = 0;
    va_end(args);
}
#else
// inline void spsc_rring_debug_log(const char *format, ...) { }
#define spsc_rring_debug_log(...) {}
#endif


// Buffer life cycle
// ------------------------------------------------------------

void new_buffer(int sz)
{
  if (g_buffer != NULL) {
    fprintf(stderr, "ERROR: tried to call new_buffer a second time\n");
    fprintf(stderr, "Only one global ring buffer permitted for now.");
    abort();
  }
  g_buffer = malloc(sz); 
  orig_buffer_end = sz;  // Need room for the largest message.
  spsc_rring_debug_log("Initialized global buffer, address %p\n", g_buffer);
}

void reset_buffer()
{
  g_buffer_end = orig_buffer_end;
}

void free_buffer()
{
  spsc_rring_debug_log("Freeing buffer %p\n", g_buffer);
  free(g_buffer);
  g_buffer = NULL;
  orig_buffer_end = -1;
}

// Buffer operations
//--------------------------------------------------------------------------------

char* peek_buffer(int* numread)
{
  while (1)
  {
    int observed_head = g_buffer_head; // We "own" the head (and _end)
    int observed_tail = g_buffer_tail;
    int observed_end  = g_buffer_end;
    // spsc_rring_debug_log(" peek_buffer: head/tail/end: %d / %d / %d\n", observed_head, observed_tail, g_buffer_end);  
    
    if( observed_head == observed_tail ) {
      *numread = 0;
      return NULL;
    }
    // If we get past here we KNOW we are in torn/wrap-around tail<head
    // state, which gives us priority to modify g_buffer_end an flip
    // back to the "normal" head<=tail state.
    
    // A shrink may have left us with nothing to read at the end here:
    if (observed_head == observed_end) {
      spsc_rring_debug_log(" !!peek_buffer: FIXUP head==end==%d, resetting it, RESTORING end\n", observed_end);
      g_buffer_end = orig_buffer_end; // Allowed to write INtorn state.
      observed_end = orig_buffer_end;
      g_buffer_head = 0; // Switch to natural state.
      observed_head = 0;
      continue;
    }

    char* start = g_buffer + observed_head;
    if ( observed_head < observed_tail ) {    
      *numread = observed_tail - observed_head;
    } else {
      spsc_rring_debug_log(" ! peek_buffer: Torn state reading just from %d to end (%d)\n",
		    observed_head, observed_end);
      *numread = observed_end - observed_head;
    }
    return start;
  }
}

void pop_buffer(int numread)
{
  int observed_head = g_buffer_head; // We "own" the head 
  int observed_end  = g_buffer_end;  // We "own" the end
  spsc_rring_debug_log(" pop_buffer: advancing head (%d) by %d\n", observed_head, numread);
  assert(numread > 0);
  if (observed_head == observed_end) {
    spsc_rring_debug_log(" !!pop_buffer: FIXUP head==end, resetting it, RESTORING end\n");
    g_buffer_end = orig_buffer_end; // Total store order!
    g_buffer_head = 0;   // Flip the state back to in-order, release "lock" on _end
    observed_head = 0;
  }
  
  if ( observed_head + numread < observed_end ) {
    g_buffer_head += numread; // Clear the read bytes.
    return;
  } else if ( observed_head + numread == observed_end ) {
    spsc_rring_debug_log(" ! pop_buffer: Wrapping head back around, RESTORING end to %d\n", orig_buffer_end);
    // Here, the tail is to our "left".  That state gives US ownership over g_buffer_end to write it:
    g_buffer_end = orig_buffer_end; // Total store order!
    g_buffer_head = 0;              // EXIT wrap-around state.
    return;
  } else {
    fprintf(stderr, "ERROR: tried to pop %d bytes past the end; head %d, tail %d, end %d",
	    numread, observed_head, g_buffer_tail, observed_end);
    abort();
  }
}


// Hacky busy-wait by thread-yielding for now:
static inline void wait()
{
#ifdef _WIN32
  SwitchToThread();
#else  
  sched_yield();
#endif
}


char* reserve_buffer(int len)
{
  if (len > orig_buffer_end) {
    fprintf(stderr,"\nERROR: reserve_buffer request bigger than allocated buffer itself! %d", len);
    abort();
  }
  while(1) // Retry loop.
    { 
    int our_tail = g_buffer_tail;
    int observed_head = g_buffer_head; // Only consumer changes this.
    int observed_end = g_buffer_end;
    int headroom;
    if (our_tail < observed_head) // Torn/wrapped-around state.
         headroom = observed_head - our_tail;
    else headroom = observed_end  - our_tail;

    spsc_rring_debug_log("  reserve_buffer: headroom = %d  (head/tail/end %d / %d / %d)\n",
          headroom, observed_head, our_tail, observed_end);
    if (len < headroom)
      {
        g_buffer_last_reserved = len;
        return g_buffer+our_tail; // good to go!
      }
    else if (our_tail < observed_head) // Torn state
      {
        int clearpos = our_tail + len;
        if ( clearpos < observed_end ) {
          // Don't wait for state change, wait till we have just enough room:
          // while( g_buffer_head < clearpos ) 
          spsc_rring_debug_log("! reserve_buffer: wait for head to advance.  Head/tail/end: %d %d %d\n",
                               observed_head, our_tail, observed_end);
        } else {
          // Otherwise we have to wait for state change.  In natural
          // state the shrunk buffer is restored.
          // while( g_buffer_head < our_tail ) 
          spsc_rring_debug_log("! reserve_buffer: wait to exit torn state.  Head/tail/end: %d %d %d\n",
                               observed_head, our_tail, observed_end);
        }
        wait();
        continue;
      }
    else // Natural state but need to switch.
      {
        // In the natural state, we may be near the _end and need to
        // shrink/wrap-early.  BUT, we cannot wrap if head is squatting at
        // the start -- that would make a full state appear empty.
        while ( observed_head == 0 ) {
          spsc_rring_debug_log("! reserve_buffer: stalling EARLY WRAP (tail %d), until head moves off the start mark\n",
                               our_tail);
          wait();
          observed_head = g_buffer_head;
        }
        
        spsc_rring_debug_log("! reserve_buffer: committing an EARLY WRAP, shrinking end from %d to %d\n",
                      observed_end, our_tail);
        // We're in "natural" not "torn" state until *we* change it.
        g_buffer_end = our_tail; // The state gives us "the lock" on this var.
        our_tail      = 0;
        g_buffer_tail = 0; // State change!  Torn state.
        continue;
      }
  }
}

void release_buffer(int len)
{
  // finished_reserve_buffer(len);
  // g_buffer_tail += g_buffer_total_reserved; // Publish it!  
  //  g_buffer_total_reserved = 0;

  spsc_rring_debug_log("  => release_buffer of %d bytes, new tail %d\n", len, g_buffer_tail + len);
  
  if (len > g_buffer_last_reserved) {
    fprintf(stderr, "ERROR: cannot finish/release %d bytes, only reserved %d\n",
            len, g_buffer_last_reserved);
    abort();
  }
  g_buffer_tail += len;
  g_buffer_last_reserved = -1;
  
  // g_buffer_msgs++; // Only a release counts as a real "message".
}
