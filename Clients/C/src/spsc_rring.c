
#include <stdio.h>
#include <stdlib.h>
#include <assert.h>
#include "ambrosia/internal/spsc_rring.h"

#if _WIN32
#else
  #include <sched.h> // sched_yield
#endif

// FIXME: replace globals with a proper API for dynamically allocating buffers:

// TODO: FACTOR OUT:
// Single-producer Single-consumer concurrent ring buffer:
char* g_buffer = NULL;
volatile int g_buffer_head = 0; // Byte offset into buffer, written by consumer.
volatile int g_buffer_tail = 0; // Byte offset into buffer, written by producer.
// volatile int g_buffer_msgs = 0; // Count of complete "messages" in g_buffer
volatile int g_buffer_end = -1; // The current capacity, MODIFIED dynamically by PRODUCER.

int orig_buffer_end = -1; // Snapshot of the original buffer capacity.

int g_buffer_last_reserved = -1; // The number of bytes in the last reserve call (producer-private)
// int g_buffer_total_reserved = -1; // The number of bytes in the last reserve call (producer-private)


// ------------------------------------------------------------

void new_buffer(int sz) {
  g_buffer = malloc(sz); 
  orig_buffer_end = sz;  // Need room for the largest message.
  spsc_rring_debug_log("Initialized global buffer, address %p\n", g_buffer);
}

void reset_buffer() {
  g_buffer_end = orig_buffer_end;
}

//--------------------------------------------------------------------------------

// (Consumer) Wait until a number of (contiguous) bytes is available within the
// buffer, and write the pointer to those bytes into the pointer argument.
// 
// This only reads in units of "complete messages", but it is UNKNOWN
// how many complete messages are returned into the buffer.
//
// RETURN: the pointer P to the available bytes.
// RETURN(param): set N to the (nonzero) number of bytes read.
// POSTCOND: the permission to read N bytes from P
// POSTCOND: the caller must use pop_buffer(N) to actually
//          free these bytes for reuse.
//
// IDEMPOTENT! Only pop actually clears the bytes.
char* peek_buffer(int* numread) {
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

// (Consumer) Free N bytes from the ring buffer, marking them as consumed and
// allowing the storage to be reused.
void pop_buffer(int numread) {
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

// From old flush_buffer:
  /*  
  // ASSUMPTION: only complete RPC messages reside in the buffer!
  // This sends out the RPCBatch header followed by the messages.
  if (g_buffer_msgs > 1) {
    spsc_rring_debug_log(" sending RPCBatch of size %d\n", g_buffer_msgs);
    char tempbuf[16];
    char* cur = tempbuf;
    int allbytes = g_buffer_tail + 1 + zigzag_int_size(g_buffer_msgs);
    cur = write_zigzag_int(cur, allbytes); // Size 
    *cur++ = RPCBatch;                   // Type
    cur = write_zigzag_int(cur, g_buffer_msgs); // RPCBatch has numMsgs 1st
    send_all(g_to_immortal_coord, tempbuf, cur-tempbuf, 0);
  }
  */  


// Hacky busy-wait by thread-yielding for now:
static inline void wait() {
#ifdef _WIN32
  SwitchToThread();
#else  
  sched_yield();
#endif
}


// (Producer) Grab a cursor for writing an (unspecified) number of bytes to the
// tail of the buffer.  It's ok to RESERVE more than you ultimately USE.
char* reserve_buffer(int len) {
  if (len > orig_buffer_end) {
    fprintf(stderr,"\nERROR: reserve_buffer request bigger than allocated buffer itself! %d", len);
    abort();
  }
  /*
  while (len > g_buffer_end) {
    spsc_rring_debug_log(" reserve_buffer: producer waiting until consumer un-shrinks the buffer..\n");
    wait();
  }
  */

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


/*
// Finish the data transfer (corresponding to the previous reserve_buffer) but
// do NOT actually release the bytes to the consumer yet. That waits until
// someone calls "release_buffer" instead of this procedure.
//
// Adds "len" bytes to the tail of the buffer.  This number must be
// less than or equal to the amount reserved.
static inline void finished_reserve_buffer(int len) {
  if (len > g_buffer_last_reserved) {
    fprintf(stderr, "ERROR: cannot finish/release %d bytes, only reserved %d\n", len, g_buffer_last_reserved);
    abort();
  }
  // Ammendment:
  g_buffer_total_reserved -= g_buffer_last_reserved;
  g_buffer_total_reserved += len;
  g_buffer_last_reserved  = -1;
  // We don't write g_buffer_tail, because we don't want the consumer to have it yet:
}
*/

// (Producer) Add "len" bytes to the tail and release the buffer.
// This number must be less than or equal to the amount reserved.
// 
// ASSUMPTION: only call release to COMPLETE a message:
void release_buffer(int len) {
  // finished_reserve_buffer(len);
  // g_buffer_tail += g_buffer_total_reserved; // Publish it!  
  //  g_buffer_total_reserved = 0;

  spsc_rring_debug_log("  => release_buffer of %d bytes, new tail %d\n", len, g_buffer_tail + len);
  
  if (len > g_buffer_last_reserved) {
    fprintf(stderr, "ERROR: cannot finish/release %d bytes, only reserved %d\n", len, g_buffer_last_reserved);
    abort();
  }
  g_buffer_tail += len;
  g_buffer_last_reserved = -1;
  
  // g_buffer_msgs++; // Only a release counts as a real "message".
}
