
// Single-producer, single-consumer ring-buffer supporting
// variable-sized byte range operations.

// Currently, this is a VERY restrictive single-purpose data structure
// that only supports ONE global ring buffer.

#ifndef SPSC_RRING_HEADER
#define SPSC_RRING_HEADER

// Buffer life cycle
// ------------------------------------------------------------

// Allocate a new buffer (presently: initialize global buffer)
void new_buffer(int sz);

// Clear the (global) buffer for reuse
void reset_buffer();

// Release the memory used by the global buffer.
void free_buffer();


// Buffer operations
//--------------------------------------------------------------------------------

// (Consumer) Free N bytes from the ring buffer, marking them as consumed and
// allowing the storage to be reused.
void  pop_buffer(int numread);

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
char* peek_buffer(int* numread);

// (Producer) Grab a cursor for writing an (unspecified) number of bytes to the
// tail of the buffer.  It's ok to RESERVE more than you ultimately USE.
char* reserve_buffer(int len); 

// (Producer) Add "len" bytes to the tail and release the buffer.
// This number must be less than or equal to the amount reserved.
// 
// ASSUMPTION: only call release to COMPLETE a message:
void  release_buffer(int len);

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

#endif
