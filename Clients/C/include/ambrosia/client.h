
#ifndef AMBROSIA_CLIENT_HEADER
#define AMBROSIA_CLIENT_HEADER

#include <stdint.h>

#ifdef _WIN32
  // #pragma comment(lib,"ws2_32.lib") //Winsock Library
#else
  #include <sys/socket.h>
  #include <stdarg.h> // va_list
#endif

// -------------------------------------------------
// Data formats used by the AMBROSIA "wire protocol"
// -------------------------------------------------

// The fixed header size used by the protocol:
#define AMBROSIA_HEADERSIZE 24

// A C struct which matches the format of the header.
// 
// The compiler shouldn't insert any padding for this one, but we use
// the pragma to make absolutely sure:
// #pragma pack(1)
struct log_hdr {
  int32_t commitID;
  int32_t totalSize; // Bytesize of whole log record, including this header.
  int64_t checksum;
  int64_t seqID;
};

// This enum is established by the wire protocol, which fixes this
// assignment of (8 bit) integers to message types.
enum MsgType { RPC=0,                       // 
	       AttachTo=1,                        // dest str
	       TakeCheckpoint=2,                  // no data
	       RPCBatch=5,                        // count, msg seq
	       Checkpoint=8,
	       InitialMessage=9,                  // Inner msg.
	       UpgradeTakeCheckpoint=10,          // no data
	       TakeBecomingPrimaryCheckpoint=11,  // no data
	       UpgradeService=12                  // no data
};


// Print extremely verbose debug output to stdout:
#define amb_dbg_fd stderr
   // ^ Non-constant initializer...

//------------------------------------------------------------------------------

// FIXME: these should become PRIVATE to the library:
extern int g_to_immortal_coord, g_from_immortal_coord;

extern int upport, downport;


// Communicates with the server to establish normal operation.
//
// ARGS: two valid socket file descriptors which must have been
// received from a call to connect_sockets.
void startup_protocol(int upfd, int downfd);

// Connect the 
void connect_sockets(int* upptr, int* downptr);

//------------------------------------------------------------------------------

// PRECONDITION: sufficient space free at output pointer.
//
// RETURN: a pointer to the byte following the bytes just written.
void* amb_write_incoming_rpc(void* buf, int32_t methodID, char fireForget, void* args, int argsLen);

// Step (1): write the header portion of the outgoing messagae (no args payload).
// This nevertheless needs to know the args LENGTH so that it can put it in the header.
void* amb_write_outgoing_rpc_hdr(void* buf, char* dest, int32_t destLen, char RPC_or_RetVal,
				 int32_t methodID, char fireForget, int argsLen);

// Write an entire, complete outgoing message into memory at the
// specified location in the buffer (first argument).
//
// RETURN: a pointer to the byte following the bytes just written.
void* amb_write_outgoing_rpc(void* buf, char* dest, int32_t destLen, char RPC_or_RetVal,
			     int32_t methodID, char fireForget, void* args, int argsLen);

// Deprecated:
// Send an RPC without any extra copies of the args.  Performs TWO send syscalls.
void amb_send_outgoing_rpc(void* tempbuf, char* dest, int32_t destLen, char RPC_or_RetVal,
			   int32_t methodID, char fireForget, void* args, int argsLen);


// Read a full log header off the socket, writing it into the provided pointer.
void amb_recv_log_hdr(int sockfd, struct log_hdr* hdr);


// TEMP - audit me
void attach_if_needed(char* dest, int destLen);


// ------------------------------------------------------------

// Variable width, Zig-zag Signed Integer Encodings
// ------------------------------------------------
// This is a common varint format used, for instance, in:
//  (1) protobufs: https://developers.google.com/protocol-buffers/docs/encoding?csw=1#types
//  (2) Avro: https://avro.apache.org/docs/1.8.1/spec.html

// Write a 32-bit integer in a sparse 1-5 byte format to the pointer,
// returning the new pointer advanced by 1-5 bytes.
// 
// PRECONDITION: at least 5 bytes free at ptr.
void* write_zigzag_int(void* ptr, int32_t value);

// Reads a 32-bit integer value into the second argument.
// Returns a new pointer value if successful, and NULL otherwise.
void* read_zigzag_int(void* ptr, int32_t* ret);

// Returns the bytesize of an encoded int, without actually doing the encoding.
// This is very useful for determining how much space is needed for a size field.
int zigzag_int_size(int32_t value);


// Debugging
//------------------------------------------------------------------------------

#ifdef AMBCLIENT_DEBUG
extern volatile int64_t amb_debug_lock;

// Helper used only below
static inline void amb_sleep_seconds(double n) {
#ifdef _WIN32
  Sleep((int)(n * 1000));
#else
  int64_t nanos = (int64_t)(10e9 * n);
  const struct timespec ts = {0, nanos};
  nanosleep(&ts, NULL);
#endif
}

static inline void amb_debug_log(const char *format, ...)
{
    va_list args;
    va_start(args, format);
    amb_sleep_seconds((double)(rand()%1000) * 0.00001); // .01 - 10 ms
#ifdef _WIN32      
    while ( 1 == InterlockedCompareExchange64(&amb_debug_lock, 1, 0) ) { }
#else
    while ( 1 == __sync_val_compare_and_swap(&amb_debug_lock, 1, 0) ) { }
#endif    
    fprintf(amb_dbg_fd," [AMBCLIENT] ");
    vfprintf(amb_dbg_fd,format, args);
    fflush(amb_dbg_fd);
    amb_debug_lock = 0;
    va_end(args);
}
#else
// inline void amb_debug_log(const char *format, ...) { }
#define amb_debug_log(...) {}
#endif


// ------------------------------------------------------------

// A standardized, cross-platform way used by this library to acquire
// the last error message from a system call.
char* amb_get_error_string();

#endif
