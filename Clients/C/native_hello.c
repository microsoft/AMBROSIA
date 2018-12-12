
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

// Extra utilities (print_hex_bytes, socket_send_all):
// #include "ambrosia/internal/bits.h"

// Library-level global variables:
// --------------------------------------------------

// General helper functions
// --------------------------------------------------------------------------------



// An example service sitting on top of AMBROSIA
//------------------------------------------------------------------------------

enum MethodTable { STARTUP_MSG_ID=32 };

// FIXME: add g_numRPCBytes as an argument to startup....
// startup a ROUND.  Called once per round.
void startup(int64_t n) {
  printf("Received message from self: %d\n", n);
  // send n-1
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

  socket_send_all(upfd, buf, bufcur-buf, 0);
  
#ifdef AMBCLIENT_DEBUG  
  amb_debug_log("  Trivial checkpoint message sent to coordinator (%lld bytes), checkpoint %d bytes\n",
                (int64_t)(bufcur-buf), strsize);
  amb_debug_log("    Message was: ");
  print_hex_bytes(amb_dbg_fd,buf, bufcur-buf); fprintf(amb_dbg_fd,"\n");
#endif
}

// Translate from untyped blobs to the multi-arity calling conventions
// of each RPC entrypoint.
void dispatchMethod(int32_t methodID, void* args, int argsLen) {
  switch(methodID) {
  case STARTUP_MSG_ID:    
    startup();
    break;

  default:
    fprintf(stderr, "ERROR: cannot dispatch unknown method ID: %d\n", methodID);
    abort();
  }
}

// Application loop (FIXME: Move into the client library!)
//------------------------------------------------------------------------------

// Handle the serialized RPC after the (Size,MsgType) have been read
// off.
// 
// ARGUMENT len: The length argument is an exact bound on the bytes
// read by this function for this message, which is used in turn to
// compute the byte size of the arguments at the tail of the payload.
char* handle_rpc(char* buf, int len) {
  if (len < 0) {
    fprintf(stderr, "ERROR: handle_rpc, received negative length!: %d", len);
    abort();
  }
  char* bufstart = buf;
  char rpc_or_ret = *buf++;             // 1 Reserved byte.
  int32_t methodID;
  buf = read_zigzag_int(buf, &methodID);  // 1-5 bytes
  char fire_forget = *buf++;            // 1 byte
  int argsLen = len - (buf-bufstart);   // Everything left
  if (argsLen < 0) {
    fprintf(stderr, "ERROR: handle_rpc, read past the end of the buffer: start %p, len %d", buf, len);
    abort();
  }
  amb_debug_log("  Dispatching method %d (rpc/ret %d, fireforget %d) with %d bytes of args...\n",
		methodID, rpc_or_ret, fire_forget, argsLen);
  dispatchMethod(methodID, buf, argsLen);
  return (buf+argsLen);
}

// The heart of the runtime: enter the processing loop and make
// up-calls into the application.
void normal_processing_loop(int upfd, int downfd)
{  
  amb_debug_log("\n        .... Normal processing underway ....\n");
  struct log_hdr hdr;
  memset((void*) &hdr, 0, AMBROSIA_HEADERSIZE);

  int round = 0;
  while (!g_client_terminating) {
    amb_debug_log("Normal processing (iter %d): receive next log header..\n", round++);
    amb_recv_log_hdr(downfd, &hdr);

    int payloadsize = hdr.totalSize - AMBROSIA_HEADERSIZE;
    char* buf = calloc(payloadsize, 1);
    recv(downfd, buf, payloadsize, MSG_WAITALL);
#ifdef AMBCLIENT_DEBUG  
    amb_debug_log("Entire Message Payload (%d bytes): ", payloadsize);
    print_hex_bytes(amb_dbg_fd,buf, payloadsize); fprintf(amb_dbg_fd,"\n");
#endif

    // Read a stream of messages from the log record:
    int rawsize = 0;
    char* bufcur = buf;
    char* limit = buf + payloadsize;
    int ind = 0;
    while (bufcur < limit) {
      amb_debug_log(" Processing message %d in log record, starting at offset %d (%p), remaining bytes %d\n",
		    ind++, bufcur-buf, bufcur, limit-bufcur);
      bufcur = read_zigzag_int(bufcur, &rawsize);  // Size
      char tag = *bufcur++;                      // Type
      rawsize--; // Discount type byte.
      switch(tag) {

      case RPC:
	amb_debug_log(" It's an incoming RPC.. size without len/tag bytes: %d\n", rawsize);
	// print_hex_bytes(bufcur,rawsize);printf("\n");
	bufcur = handle_rpc(bufcur, rawsize);
	break;

      case InitialMessage:
	amb_debug_log(" Received InitialMessage back from server.  Processing..\n");
	// FIXME: InitialMessage should be an arbitrary blob... but here we're following the convention that it's an actual message.
	break;

      case RPCBatch:
	{ int32_t numMsgs = -1;
	  bufcur = read_zigzag_int(bufcur, &numMsgs);
	  amb_debug_log(" Receiving RPC batch of %d messages.\n", numMsgs);
	  char* batchstart = bufcur;
	  for (int i=0; i < numMsgs; i++) {
	    amb_debug_log(" Reading off message %d/%d of batch, current offset %d, bytes left: %d.\n",
			  i+1, numMsgs, bufcur-batchstart, rawsize);
	    char* lastbufcur = bufcur;
	    int32_t msgsize = -100;
	    bufcur = read_zigzag_int(bufcur, &msgsize);  // Size (unneeded)	    
	    char type = *bufcur++;                     // Type - IGNORED
	    amb_debug_log(" --> Read message, type %d, payload size %d\n", type, msgsize-1);
	    bufcur = handle_rpc(bufcur, msgsize-1);
	    amb_debug_log(" --> handling that message read %d bytes off the batch\n", (int)(bufcur - lastbufcur));
	    rawsize -= (bufcur - lastbufcur);
	  }
	}
	break;

      case TakeCheckpoint:
	send_dummy_checkpoint(upfd);
	break;
      default:
	fprintf(stderr, "ERROR: unexpected or unrecognized message type: %d", tag);
	abort();
	break;
      }
    }
  }
  

  g_trials_remaining--;
  if (g_trials_remaining == 0) {
    printf(" *** processing loop: Last trial finished; exiting.\n");
    if (! g_is_sender) {
      printf("Receiver exiting after a moderate wait...\n");
      sleep_seconds(30); // Uh.....
    }
    exit(0);
  } else {
    printf(" *** startup: Beginning next trial; remaining: %d.\n", g_trials_remaining);
  }
  return;
}



// Basic example application
// ------------------------------------------------------------



// Reset global state for the next trial.
void reset_trial_state() {    
  g_numRPCBytes = maxMessageSize; // Initial round size.

  reset_buffer(); // TEMP / FIXME

  g_startTimeRound = 0.0;
  g_totalExpected = -1;
  
  if (PREFILL) g_is_dummy_round = 0;
  g_waiting_final_ack = 0;
  g_client_terminating = 0;

  // HACKY - twisting stuff here to work for pingpong too:
  if (g_pingpong_mode) {
    set_total_expected();
    PREFILL = 0;
    bytesPerRound = 1;
    g_is_dummy_round = 0;    
    g_numRPCBytes = 1;
    g_pingpong_count = 0;
    if (g_pingpong_latencies != NULL) free(g_pingpong_latencies);
    g_pingpong_latencies = (double*)calloc(sizeof(double), g_total_pingpongs);
  }
}


int main(int argc, char** argv)
{
  // How big to allocate the buffer:
  int buffer_bytes_allocated = -1; // Ivar semantics - write once.  
  int upport, downport;
  
  srand(time(0));
  
  printf("Begin simple native-client experiment, interacting with ImmortalCoordinator...\n");

  if (argc == 8) {
    buffer_bytes_allocated = 1 << atoi(argv[7]);
    printf(" *** Overriding default bufsize to %d.\n", buffer_bytes_allocated);
    argc--;
  } else {
    int maxPlus = maxMessageSize + 64; // Allow for header.
    buffer_bytes_allocated = AMBCLIENT_DEFAULT_BUFSIZE > maxPlus ?
                               AMBCLIENT_DEFAULT_BUFSIZE : maxPlus;
  }
  if (argc == 7) {
    g_trials_remaining = atoi(argv[6]);
    printf(" *** Running experiment repeatedly for %d trials.\n", g_trials_remaining);
    argc--;
  }
  if (argc == 6) {
    bytesPerRound = 1 << atoi(argv[5]);
    argc--;
  }
  if (argc == 5) {
    switch (atoi(argv[1])) {
    case 0: g_is_sender = 1; g_pingpong_mode = 0;  break;
    case 1: g_is_sender = 0; g_pingpong_mode = 0;  break;
    case 2: g_is_sender = 1; g_pingpong_mode = 1;  break;
    case 3: g_is_sender = 0; g_pingpong_mode = 1;  break;
    }
    // destName = ""; // Test of send to SELF.
    destName = argv[2];
    destLen = strlen(destName); 
    upport = atoi(argv[3]);
    downport = atoi(argv[4]);
    
  } else {
    fprintf(stderr, "Usage: this executable expects args: <role=0/1/2/3> <destination> <port> <port> [roundsz] [trials] [bufsz]\n");
    fprintf(stderr, "  where <role> is 0/1 for sender/receiver throughput mode\n");
    fprintf(stderr, "     OR <role> is 2/3 for sender/receiver ping-pong mode\n");
    fprintf(stderr, "  where <destination> is e.g. 'native1' or 'native2' and is the name of the OTHER party\n");
    fprintf(stderr, "  optional [roundsz] argument is the log base 2 of bytes-per-round, default 30\n");
    fprintf(stderr, "  optional [trials] argument repeats the entire experiment\n");    
    fprintf(stderr, "  optional [bufsz] is the log base 2 of the buffer byte size\n");
    fprintf(stderr, "  \n");    
    fprintf(stderr, "  NOTE: in ping-pong mode [roundsz] determines the number of pingpongs written to pingpongs.txt\n");
    abort();
  }

  if (bytesPerRound <= maxMessageSize && !g_pingpong_mode) {
    fprintf(stderr, "\nERROR: Bytes-per-round should be bigger than max message size.\n");
    abort();
  }

  if ( g_is_sender || destLen == 0) 
    printf("We are running the SENDER\n");
  else
    printf("We are running the RECEIVER\n");
  
  printf("Connecting to my coordinator on ports: %d (up), %d (down)\n", upport, downport);
  printf("The 'up' port we connect, and the 'down' one the coordinator connects to us.\n");
  /* printf("Please make sure that you have already registered the service in Azure tables with commands such as the following:\n"); */
  /* printf("  Ambrosia/bin/x64/Release/net46/LocalAmbrosiaRuntime.exe  native1 50000 50001 native1 logs/ nativetestbins a n y 1000 n 0 0\n"); */
  /* printf("  Ambrosia/bin/x64/Release/net46/LocalAmbrosiaRuntime.exe  native2 50002 50003 native2 logs/ nativetestbins a n y 1000 n 0 0\n"); */
  /* printf("(You need four ports, in the above example: 50000-50003 .)\n"); */

  int upfd, downfd;
  connect_sockets(upport, downport, &upfd, &downfd);
  amb_debug_log("Connections established (%d,%d), beginning protocol.\n", upfd, downfd);
  startup_protocol(upfd, downfd);

  g_to_immortal_coord = upfd;
  g_from_immortal_coord = downfd;

  new_buffer(buffer_bytes_allocated);
  
  reset_trial_state();

  printf(" *** (starting) BUFFER SIZE: %d\n", buffer_bytes_allocated);
  printf(" *** BYTES PER ROUND: %ld\n", (long int)bytesPerRound);
  printf(" *** SEND_ACK: %d\n", SEND_ACK);
  printf(" *** PREFILL: %d\n", PREFILL);
  printf(" *** PINGPONG mode: %d\n", g_pingpong_mode);  
  printf(" *** startup: Beginning experiment, first trial of: %d.\n", g_trials_remaining);
  if ( g_is_sender || destLen == 0)
    printf("Bytes per RPC,  Throughput (GiB/sec),  Round-Time,  Round-Msgs\n");  

#ifdef _WIN32
  DWORD lpThreadId;
  HANDLE th = CreateThread(NULL, 0,
			   network_progress_thread,
			   NULL, 0,
			   & lpThreadId);
  if (th == NULL)
#else
  pthread_t th;
  int res = pthread_create(& th, NULL, network_progress_thread, NULL);
  if (res != 0)
#endif
  {
    fprintf(stderr, "ERROR: failed to create network progress thread.\n");
    abort();
  }

  while (g_trials_remaining > 0) {
    normal_processing_loop(upfd,downfd);
    reset_trial_state();
    // First time we are driven by a startup message from LAR.  Subsequently we run it ourselves:
    startup();
  }
  printf("Done.\n");
}
