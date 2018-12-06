// A simple service that speaks the AMBROSIA runtime protocol.

#include <stdio.h>
#include <stdlib.h>

// #include <stdint.h>

#include <string.h>
#include <sys/types.h> // MSG_WAITALL
#include <time.h> // nanosleep
#include <assert.h>
/* #include <errno.h> */

#ifdef _WIN32
/*   #define WIN32_LEAN_AND_MEAN */
//  #include <malloc.h> 
/*   #include<winsock2.h> */
/*   #include <Ws2tcpip.h> */

/*   // for SIO_LOOPBACK_FAST_PATH: */
/*   #include <Mstcpip.h>  */

/*   #pragma comment(lib,"ws2_32.lib") //Winsock Library */

/*   #define int32_t  INT32 */
/*   #define uint32_t UINT32 */
/*   #define int64_t  INT64 */
/*   #define uint64_t UINT64 */

#else

/* // *nix, but really Linux only for now: */
  #include <alloca.h>
//  #include <unistd.h>
//  #include <sys/socket.h> 
// #include <netinet/in.h>
//  #include <arpa/inet.h> // inet_pton
//  #include <netdb.h>
//  #include <stdarg.h>
   #include <pthread.h> 
#endif


// TODO: remove internal dependency:
#include "ambrosia/internal/spsc_rring.h"
#include "ambrosia/client.h"


// Library-level global variables:
// --------------------------------------------------

// FINISHME: need to snapshot global state in a checkpoint.


int g_is_sender = -1;     // IVar semantics - set once.
int g_pingpong_mode = 0;  // IVar semantics - set once.
int g_pingpong_count = 0;
int g_total_pingpongs = 20000;
double* g_pingpong_latencies = NULL; // Array of latencies.

int g_waiting_final_ack = 0;

// An INTERNAL global representing whether the client is terminating this AMBROSIA endpoint.
int g_client_terminating = 0;

int g_is_dummy_round = 1; // We do one dummy round before starting the measurement rounds.
int g_trials_remaining = 1; // Perform the whole experiment multiple times.

int64_t g_totalExpected = -1; // Expected messages.

double g_startTimeRound = 0.0;
int g_numRPCBytes = 0;


// Library-level Global constants
// --------------------------------------------------

// A Gibibyte (binary not metric/decimal) for now:
#define ONE_GIBIBYTE ((int64_t)1 * 1024 * 1024 * 1024)

const int64_t maxMessageSize = 2097152; // Must be a power of 2.
const int64_t minMessageSize = 16;      // Must be a power of 2.

int64_t bytesPerRound = (ONE_GIBIBYTE); // The "standard" measurement we do.

// Boolean: send an ACK packet back (or not)
#define SEND_ACK 0

// Send N messages to fill up the pipe BEFORE sending N messages to time throughput
// (don't then do an extra dummy round)
int PREFILL = 1;

// Print a (slightly verbose) additional set of messages.
int g_moderate_chatter = 1; // Boolean.


// TEMP hacks: single, global destination
char* destName; // Initialized below..
int destLen;    // Initialized below..



// General helper functions
// --------------------------------------------------------------------------------

// Hacky busy-wait by thread-yielding for now:
static inline void yield_thread() {
#ifdef _WIN32
  SwitchToThread();
#else  
  sched_yield();
#endif
}

void sleep_seconds(double n) {
#ifdef _WIN32
  Sleep((int)(n * 1000));
#else
  int64_t nanos = (int64_t)(10e9 * n);
  const struct timespec ts = {0, nanos};
  nanosleep(&ts, NULL);
#endif
}

#ifdef _WIN32
double current_time_seconds()
{
    LARGE_INTEGER frequency;
    LARGE_INTEGER current;
    double result;
    QueryPerformanceFrequency(&frequency);
    QueryPerformanceCounter(&current);
    return (double)current.QuadPart / (double)frequency.QuadPart;
}
#else
double current_time_seconds() {
  struct timespec current;
  clock_gettime((clockid_t)CLOCK_REALTIME, &current);
  // clock_gettime(0,NULL);  
  return  (double)current.tv_sec + ((double)current.tv_nsec * 0.000000001);
}
#endif


// An example service sitting on top of AMBROSIA
//------------------------------------------------------------------------------

enum MethodTable { STARTUP_ID=32, TPUT_MSG_ID=33, ACK_MSG_ID=34 };

// RPC proxies for remote methods:
void send_ack();

void receive_ack(int numRPCBytes);
void end_round(int numRPCBytes);

// Call send_message in a loop.
void send_loop( int numRPCBytes )
{  
  int64_t iterations = bytesPerRound / numRPCBytes;
 
  char* tempbuf = (char*)malloc(1 + 5 + destLen + 1 + 5 + 1 + numRPCBytes);
  char* RPCbuf = NULL;

  attach_if_needed(destName, destLen); // Hard-coded global dest name.
  
  int64_t rep = 0;
  // This is our warm-up phase:
  if(PREFILL) rep = -iterations;
  
  for(; rep < iterations; rep++) {
    // When we hit zero this is our "logically first" iteration:
    if(rep==0) g_startTimeRound = current_time_seconds();
    //      buffer_outgoing_rpc_hdr(destName, destLen, 0, TPUT_MSG_ID, 1, numRPCBytes);      
    //      char* cur = reserve_buffer(numRPCBytes);
    {
	int sizeBound = (1 // type tag
			 + 5 + destLen + 1  // RPC_or_RetVal
			 + 5 + 1 // fireForget
			 + 5 + numRPCBytes);
	char* start = reserve_buffer(sizeBound);
	char* cur = amb_write_outgoing_rpc_hdr(start, destName,destLen, 0, TPUT_MSG_ID, 1, numRPCBytes);
	for(int i=0; i<numRPCBytes; i++) *cur++ = (char)i;
        // ^ TODO: may want to memcpy instead (like PerformanceTestInterruptable)
	release_buffer(cur-start); // Let the consumer have these bytes.
    }
  }
  
  double duration = current_time_seconds() - g_startTimeRound;
  double throughput = ((double)iterations*numRPCBytes / (double)ONE_GIBIBYTE) / duration;
  if (g_moderate_chatter)
    printf("  Optimistic throughput based on the just sender's time to get the messages out the door:\n");
  if (!g_pingpong_mode)
    printf(" *X*  %s  %d\t %lf\t %lf\t %ld\n", SEND_ACK ? "BEFORE_ACK" : "",
	   numRPCBytes, throughput, duration, (long int)iterations);
  fflush(stdout);
  free(tempbuf);

  // If we're not in ACK-waiting mode, we just call this directly to simulate an ACK:
  if (!SEND_ACK && !g_pingpong_mode) end_round(numRPCBytes);
  return;
}

// Set the g_numRPCBytes for the next round.
int advance_round() {
  if (g_pingpong_mode) {
    if (g_pingpong_count < g_total_pingpongs) 
      return 1;
    else return 0;    
  } else if (g_numRPCBytes > minMessageSize) {
    if (g_is_dummy_round) {
      printf("  ** END DUMMY ROUND ** \n\n");
      g_is_dummy_round = 0;
    } else
      g_numRPCBytes /= 2;
    return 1;
  } else {
    return 0;
  }
}

static inline void set_total_expected() {
  if (g_pingpong_mode) {
    g_totalExpected = 1;
    return;
  }  
  g_totalExpected = bytesPerRound / g_numRPCBytes;
  if (PREFILL) {
    g_totalExpected *= 2;
    amb_debug_log("because of PREFILL mode, expecting double (%d) messages", g_totalExpected);
  }
  assert(g_totalExpected >= 1);
}

/* void exit_or_restart() { */
/*   g_trials_remaining--; */
/*   if (g_trials_remaining == 0) */
/*     exit(0); */
/*   else { */
/*     fprintf(stderr," ERROR: FINISHME\n"); */
/*     abort(); */
/*   } */
/* } */

// Receiver side.
void receive_message(char* msg, int64_t len) {
  g_totalExpected--;
  
  amb_debug_log("GOT THE MESSAGE: %ld bytes, %ld remaining expected messages this round\n", len, g_totalExpected);
#ifdef AMBCLIENT_DEBUG
  if ( len != g_numRPCBytes ) {
    fprintf(stderr,"\nError: expected message of size %d this round, received %lld\n", g_numRPCBytes, len);
    abort();
  }
  for(int i=0; i<len; i++) {
    if (msg[i] != (char)i) {
      fprintf(stderr,"\nError: byte %d of received message was %d, expected %d\n", i, msg[i], i);
      abort();
    }
  }
#endif

  if(g_totalExpected == 0) {
    amb_debug_log(" That's all the expected messages this round.\n");
    if (SEND_ACK || g_pingpong_mode) {
      send_ack();
      amb_debug_log("Sent ACK.\n");
    }
    if (advance_round()) {
      set_total_expected();
      if(!g_pingpong_mode)
	printf("Receiver starting next round with message size %d, expected messages %ld.\n",
	       g_numRPCBytes, (long int)g_totalExpected);
    } else {
      printf("Finished last round of this experiment\n");
      if (! SEND_ACK) {
	printf("Since we are not sending ACKs of every round, send a final shut-down ACK..\n");
	send_ack();
      }
      g_client_terminating = 1; // exit_or_restart();
    }
  }
}

// FIXME: add g_numRPCBytes as an argument to startup....
// startup a ROUND.  Called once per round.
void startup() {
  if (g_is_sender || destLen == 0) {
    if (g_moderate_chatter) printf("   Sender starting this round, g_numRPCBytes = %d\n", g_numRPCBytes);
    send_loop( g_numRPCBytes );
    if (g_moderate_chatter) printf("   send_loop finished, waiting for ACK...\n");    
  } else {
    set_total_expected();
    printf("Receiver starting 1st round, msg size %d, expected messages %ld.\n",
	   g_numRPCBytes, (long int)g_totalExpected);
  }
}

// Mutates: g_client_terminating.
// Can send a startup message to ourselves to continue the next round.
void end_round(int numRPCBytes) {
  if (g_moderate_chatter && SEND_ACK)
    printf("  Finished this round (message size %d)\n\n", g_numRPCBytes);
  long iterations = bytesPerRound / numRPCBytes;
  double duration = current_time_seconds() - g_startTimeRound;
  double throughput = ((double)iterations * numRPCBytes / (double)ONE_GIBIBYTE) / duration;
  if (g_moderate_chatter) printf("  Realistic throughput based on time for a final acknowledgement from:\n");
  if (SEND_ACK || g_pingpong_mode) {
    printf(" *X*  AFTER_ACK   %d\t %lf\t %lf\t %ld\n", numRPCBytes, throughput, duration, iterations);
    fflush(stdout);
  }

  // Tail call to the next round.
  if (advance_round()) {
    if (g_moderate_chatter) printf("receive ACK: bouncing a startup message to ourselves\n");
    char sendbuf[16];
    char* endbuf = amb_write_outgoing_rpc(sendbuf, "", 0, 0, STARTUP_ID, 1, NULL,0);

    // Audit: is this needed?
    socket_send_all(g_to_immortal_coord, sendbuf, endbuf-sendbuf, 0);
  } else {
    if (SEND_ACK) {
      printf("Finished last round, exiting...\n");
      g_client_terminating = 1; // exit_or_restart();
    } else {
      printf("Finished last round, waiting for shutdown ACK (SEND_ACK on each round is off)\n");
      g_waiting_final_ack = 1;
    }
  }
}

// Sender side.
void receive_ack(int numRPCBytes) {
  if (g_waiting_final_ack) {
    printf("  Sender received final shutdown ACK, shutting down\n");
    g_client_terminating = 1; // exit_or_restart();
  }
  else if (g_pingpong_mode) {
    assert(numRPCBytes == 1);
    double duration = current_time_seconds() - g_startTimeRound;
    assert(g_pingpong_count < g_total_pingpongs);
    g_pingpong_latencies[g_pingpong_count] = duration;
    amb_debug_log("Logged result from ping pong %d: %lf\n", g_pingpong_count, duration);    
    g_pingpong_count++;
    
    if (advance_round()) {
      amb_debug_log("advance_round said we should do more pingpongs, call send_loop\n");
      send_loop(1);
    } else {
      printf("Time to shut down these ping-pongs..\n");
      printf("Last 10000 Microsecond latencies:\n");
      int start = g_total_pingpongs - 10000;
      if (start < 0) start=0;
      for(int i= start; i<g_total_pingpongs; i++) {
	printf("%d ", (int)(g_pingpong_latencies[i] * 1000.0 * 1000.0));
	if(i % 500 == 0) fflush(stdout);
      }
      printf("\n");
      fflush(stdout);
      exit(0); // HACK
    }
  } else if (SEND_ACK)
    end_round(numRPCBytes);
  else {
    fprintf(stderr, "The imposssible happened.\n");
    abort();
  }
}

/*
// PING-PONG between two endpoints:
// A more complicated method that sends messages.
void bounce(int64_t n) {
  if(n == 0) printf("Done bouncing!\n");
  else {
    printf("Still bouncing (%lld)\n", n);
    send_bounce(destName, n-1);    
  }
}
*/

// Everything in this section should, in principle, be automatically GENERATED:
//------------------------------------------------------------------------------

void send_ack() {
  char sendbuf[16];
  char* newpos = amb_write_outgoing_rpc(sendbuf, destName, destLen, 0, ACK_MSG_ID, 1, NULL, 0);
  socket_send_all(g_to_immortal_coord, sendbuf, newpos-sendbuf, 0);
}

void send_dummy_checkpoint(int upfd) {
  const char* dummy_checkpoint = "dummy_checkpoint";
  int size = 1 + strlen(dummy_checkpoint);
  char* buf = alloca(size+5);
  char* bufcur = write_zigzag_int(buf, size); // Size (including type tag)
  *bufcur++ = Checkpoint;             // Type
  bufcur += sprintf(bufcur, "%s", dummy_checkpoint); // Dummy checkpoint.
  socket_send_all(upfd, buf, bufcur-buf, 0);
#ifdef AMBCLIENT_DEBUG  
  amb_debug_log("  Trivial checkpoint message sent to coordinator (%lld bytes)\n",
		(int64_t)(bufcur-buf));
  amb_debug_log("    Message was: ");
  print_hex_bytes(amb_dbg_fd,buf, bufcur-buf); fprintf(amb_dbg_fd,"\n");
#endif
}


// Translate from untyped blobs to the multi-arity calling conventions
// of each RPC entrypoint.
void dispatchMethod(int32_t methodID, void* args, int argsLen) {
  switch(methodID) {
  case STARTUP_ID:    
    startup();
    break;

  case TPUT_MSG_ID:
    receive_message( (char*)args, argsLen );
    break;
    
  case ACK_MSG_ID:
    receive_ack( g_numRPCBytes );
    break;

  default:
    fprintf(stderr, "ERROR: cannot dispatch unknown method ID: %d\n", methodID);
    abort();
  }
}

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

// Application loop (FIXME: Move into the client library!)
//------------------------------------------------------------------------------


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


#ifdef _WIN32
DWORD WINAPI network_progress_thread( LPVOID lpParam )
#else
void* network_progress_thread( void* lpParam )
#endif
{
  printf(" *** Network progress thread starting...\n");
  int hot_spin_amount = 1; // 100
  int spin_tries = hot_spin_amount;
  while(1) {
    int numbytes = -1;
    char* ptr = peek_buffer(&numbytes);    
    if (numbytes > 0) {
      amb_debug_log(" network thread: sending slice of %d bytes\n", numbytes);
      socket_send_all(g_to_immortal_coord, ptr, numbytes, 0);
      pop_buffer(numbytes); // Must be at least this many.
      spin_tries = hot_spin_amount;
    } else if ( spin_tries == 0) {
      spin_tries = hot_spin_amount;
      // amb_debug_log(" network thread: yielding to wait...\n");
 #ifdef AMBCLIENT_DEBUG      
      sleep_seconds(0.5);
      sleep_seconds(0.05);
#endif
      yield_thread();
    } else spin_tries--;   
  }

  return 0;
}


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
  
  printf("Connecting to my coordinator on ports: %d, %d\n", upport, downport);
  printf("Please make sure that you have already registered the service in Azure tables with commands such as the following:\n");
  printf("  Ambrosia/bin/x64/Release/net46/LocalAmbrosiaRuntime.exe  native1 50000 50001 native1 logs/ nativetestbins a n y 1000 n 0 0\n");
  printf("  Ambrosia/bin/x64/Release/net46/LocalAmbrosiaRuntime.exe  native2 50002 50003 native2 logs/ nativetestbins a n y 1000 n 0 0\n");
  printf("(You need four ports, in the above example: 50000-50003 .)\n");

  int upfd, downfd;
  connect_sockets(&upfd, &downfd);
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
