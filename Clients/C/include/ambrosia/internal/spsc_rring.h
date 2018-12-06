
// Single-producer, single-consumer ring-buffer supporting
// variable-sized byte range operations.

#ifndef SPSC_RRING_HEADER
#define SPSC_RRING_HEADER

// FIXME: allocate and return a new buffer struct (right now: initialize global buffer)
void new_buffer(int sz);

// TEMP: REMOVEME
void reset_buffer();

void  pop_buffer(int numread);
char* peek_buffer(int* numread);
char* reserve_buffer(int len); 
void  release_buffer(int len);


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
