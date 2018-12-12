
// -----------------------------------------------------------------------------
// A "hello world" service that sends a message to itself through AMBROSIA.
// -----------------------------------------------------------------------------

// Linux only.

#include <stdio.h>
#include <stdlib.h>

#include <string.h>
#include <sys/types.h> 
#include <time.h> 
#include <assert.h>

#ifdef _WIN32
#error "Windows not finished"
#else
  // Unix variants, but really just Linux for now:
  #include <alloca.h>
  #include <pthread.h> 
#endif

// #include "ambrosia/internal/spsc_rring.h"
#include "ambrosia/client.h"

#include "ambrosia/internal/bits.h" // amb_socket_send_all


// An example service sitting on top of AMBROSIA
//------------------------------------------------------------------------------

enum MethodTable { STARTUP_MSG_ID=32 };

// FIXME: add g_numRPCBytes as an argument to startup....
// startup a ROUND.  Called once per round.
void startup(int64_t n) {
  printf("\nHello! Received message from self: %d\n", n);
  // TODO: send n-1 and count down...

  printf("\nSignaling shutdown to runtime...\n", n);
  amb_shutdown_client_runtime(); 
}

// Everything in this section should, in principle, be automatically GENERATED:
//------------------------------------------------------------------------------

void send_dummy_checkpoint(int upfd) {
  const char* dummy_checkpoint = "dummyckpt";
  int strsize = strlen(dummy_checkpoint);

  // New protocol, the payload is just a 64 bit size:
  int   msgsize = 1 + 8;
  char* buf = alloca(msgsize + 5 + strsize);
  char* bufcur = write_zigzag_int(buf, msgsize); // Size (including type tag)
  *bufcur++ = Checkpoint;                        // Type
  *((int64_t*)bufcur) = strsize;                 // 8 byte size
  bufcur += 8;
  
  assert(bufcur-buf == 9 + zigzag_int_size(msgsize)); 

  // Then write the checkpoint itself AFTER the regular message:
  bufcur += sprintf(bufcur, "%s", dummy_checkpoint); // Dummy checkpoint.

  amb_socket_send_all(upfd, buf, bufcur-buf, 0);
  
#ifdef AMBCLIENT_DEBUG  
  amb_debug_log("  Trivial checkpoint message sent to coordinator (%lld bytes), checkpoint %d bytes\n",
                (int64_t)(bufcur-buf), strsize);
  amb_debug_log("    Message was: ");
  print_hex_bytes(amb_dbg_fd,buf, bufcur-buf); fprintf(amb_dbg_fd,"\n");
#endif
}


// Translate from untyped blobs to the multi-arity calling conventions
// of each RPC entrypoint.
void amb_dispatch_method(int32_t methodID, void* args, int argsLen) {
  switch(methodID) {
  case STARTUP_MSG_ID:    
    startup(10);
    break;

  default:
    fprintf(stderr, "ERROR: cannot dispatch unknown method ID: %d\n", methodID);
    abort();
  }
}


// Basic example application
// ------------------------------------------------------------

int main(int argc, char** argv)
{
  printf("Begin Hello-World AMBROSIA + native-client\n");  
  
  int upport = 1000, downport = 1001;
  if (argc >= 2) upport   = atoi(argv[1]);
  if (argc >= 3) downport = atoi(argv[2]);

  printf("Connecting to my coordinator on ports: %d (up), %d (down)\n", upport, downport);
  printf("The 'up' port we connect, and the 'down' one the coordinator connects to us.\n");
  amb_initialize_client_runtime(upport, downport, 0);
  // ^ Calls callbacks for reading checkpoint and sending init message.

  // Enter processing loop until a message handler calls shutdown.
  amb_normal_processing_loop();  
  printf("\nReturned from AMBROSIA message processing loop.  All done.\n");
}
