
#ifndef AMBROSIA_CLIENT_HEADER
#define AMBROSIA_CLIENT_HEADER

#include <stdint.h>

#ifdef _WIN32
  // #pragma comment(lib,"ws2_32.lib") //Winsock Library
#else
  #include <sys/socket.h>
  #include <stdarg.h> // va_list
#endif

// #include "ambrosia/internal/bits.h"

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


// Communicates with the server to establish normal operation.
//
// ARGS: two valid socket file descriptors which must have been
// received from a call to amb_connect_sockets.
void amb_startup_protocol(int upfd, int downfd);

// Connect to the ImmortalCoordinator.  Use the provided ports.
// 
// On the "up" port we connect, and on "down" the coordinator connects
// to us.  This function writes the file descriptors for the opened
// connections into the pointers provided as the last two arguments.
void amb_connect_sockets(int upport, int downport, int* up_fd_ptr, int* down_fd_ptr);

// Encoding and Decoding message types
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


//------------------------------------------------------------------------------

// USER DEFINED: FIXME: REPLACE W CALLBACK
extern void send_dummy_checkpoint(int upfd);

// USER-DEFINED: FIXME: turn into a callback (currently defined by application):
extern void amb_dispatch_method(int32_t methodID, void* args, int argsLen);


// TEMP - audit me - need to add a hash table to track attached destinations:
void attach_if_needed(char* dest, int destLen);

//------------------------------------------------------------------------------

// PHASE 1/3
//
// This performs the full setup process: attaching to the Immortal
// Coordinator on the specified ports, creating a network progress
// thread in the background, and executing the first phases of the
// App/Coordinator communication protocol.
//
// ARG: upport: the port on which we will reach out and connect to the
//      coordinator on localhost (127.0.0.1 or ::1).  This is used to
//      send data to the coordinator.
//
// ARG: downport: (after upport is connected) the port on which we
//      will listen for the coordinator to connect to us.  This is
//      used to receive data from the coordinator.
//
// ARG: bufSz: the size of the buffer used to buffer small messages on
//      their way to the ImmortalCoordinator.  If this is zero, or
//      negative, a default is used.
//
// ARG: 
//
// RETURNS:
//
// EFFECTS:
void amb_initialize_client_runtime(int upport, int downport, int bufSz);

// PHASE 2/3
//
// The heart of the runtime: enter the processing loop.  Read log
// entries from the coordinator and make "up-calls" (callbacks) into
// the application when we receive incoming messages.  These call
// backs in turn send outgoing messages, and so on.
void amb_normal_processing_loop();

// PHASE 3/3
//
// This can be called by the client application at any time post
// initalization.  It signals that the main event loop
// (amb_normal_processing_loop) should exit.
//
// It does NOT transfer control away from the current function
// (longjmp), rather it returns to the caller, which is expected to
// return normally to the event handler loop.
void amb_shutdown_client_runtime();


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

extern void amb_sleep_seconds(double n);

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
