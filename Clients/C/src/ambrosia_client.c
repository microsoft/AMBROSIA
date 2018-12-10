
#include <stdio.h>
#include <stdlib.h>
#include <stdint.h>

#include <string.h>
/* #include <sys/types.h> */
#include <time.h>
#include <assert.h>
#include <errno.h>

#ifdef _WIN32
  #define WIN32_LEAN_AND_MEAN
  /* #include <malloc.h> */
  /* #include<stdio.h> */
  /* #include<winsock2.h> */
  /* #include <Ws2tcpip.h> */

  // for SIO_LOOPBACK_FAST_PATH: 
  #include <Mstcpip.h> 
  #pragma comment(lib,"ws2_32.lib") //Winsock Library

  /* #define int32_t  INT32 */
  /* #define uint32_t UINT32 */
  /* #define int64_t  INT64 */
  /* #define uint64_t UINT64 */

#else
  // *nix, but really Linux only for now:
  /* #include <alloca.h> */
  /* #include <unistd.h> */
  #include <sys/socket.h>
  // #include <netinet/in.h>
  #include <arpa/inet.h> // inet_pton
  #include <netdb.h> // gethostbyname
  /* #include <stdarg.h> */
#endif

#include "ambrosia/client.h"
#include "ambrosia/internal/bits.h"

// Library-level global variables:
// --------------------------------------------------

// FIXME: looks like we need a hashtable after all...
int g_attached = 0;  // For now, ONE destination.


// This follows the rule that the RECV side acts as the server:
int upport   = 1000; // Send. Up to the reliability-coordinator-as-server
int downport = 1001; // Recv. Down from the coordinator (we're server)


// Global variables that should be initialized once for the library.
// We can ONLY ever have ONE reliability coordinator.
int g_to_immortal_coord, g_from_immortal_coord;

#ifdef IPV4
const char* coordinator_host = "127.0.0.1";
#elif defined IPV6
// char* host = "0:0:0:0:0:0:0:1";
// char* host = "1:2:3:4:5:6:7:8";
const char* coordinator_host = "::1";
#else
#error "Preprocessor: Expected IPV4 or IPV6 to be defined."
#endif

#ifdef AMBCLIENT_DEBUG
// volatile int64_t debug_lock = 0;
#endif


// Reusable code for interacting with AMBROSIA
// ==============================================================================

// General helper functions
// ------------------------

// This may leak, but we only use it when we're bailing out with an error anyway.
char* amb_get_error_string() {
#ifdef _WIN32
  // TODO: could use FormatMessage here...
  char* err = (char*)malloc(2048);
  sprintf(err, "%d", WSAGetLastError());
  return err;
#else  
  return strerror(errno);
#endif  
}
    
void print_hex_bytes(FILE* fd, char* ptr, int len) {
  const int limit = 100; // Only print this many:
  fprintf(fd,"0x");
  int j;
  for (j=0; j < len && j < limit; j++) {
    fprintf(fd,"%02hhx", (unsigned char)ptr[j]);
    if (j % 2 == 1)
      fprintf(fd," ");
  }
  if (j<len) fprintf(fd,"...");
}

void print_decimal_bytes(char* ptr, int len) {
  const int limit = 100; // Only print this many:
  int j;
  for (j=0; j < len && j < limit; j++) {
    printf("%02d", (unsigned char)ptr[j]);
    if (j % 2 == 1)  printf(" ");
    else printf(".");
  }
  if (j<len) printf("...");
}


void* write_zigzag_int(void* ptr, int32_t value) {
  char* bytes = (char*)ptr;
  uint32_t zigZagEncoded = (uint32_t)((value << 1) ^ (value >> 31));
  while ((zigZagEncoded & ~0x7F) != 0) {
    *bytes++ = (char)((zigZagEncoded | 0x80) & 0xFF);
    zigZagEncoded >>= 7;
  }
  *bytes++ = (char)zigZagEncoded;
  return bytes;
}

void* read_zigzag_int(void* ptr, int32_t* ret) {
  char* bytes = (char*)ptr;
  uint32_t currentByte = *bytes; bytes++;
  char read = 1;
  uint32_t result = currentByte & 0x7FU;
  int32_t  shift = 7;
  while ((currentByte & 0x80) != 0) {    
    currentByte = *bytes; bytes++;
    read++;
    result |= (currentByte & 0x7FU) << shift;
    shift += 7;
    if (read > 5) return NULL; // Invalid encoding.
  }
  *ret = (int32_t) ((-(result & 1)) ^ ((result >> 1) & 0x7FFFFFFFU));
  return (void*)bytes;
}

int zigzag_int_size(int32_t value) {
  int retVal = 0;
  uint32_t zigZagEncoded = ((value << 1) ^ (value >> 31));
  while ((zigZagEncoded & ~0x7F) != 0) {
      retVal++;
      zigZagEncoded >>= 7;
  }
  return retVal+1;
}


// AMBROSIA-specific messaging utilities
// -------------------------------------

// FIXME - need to match what's in the AMBROSIA code.
int32_t checksum(int32_t initial, char* buf, int n) {
  int32_t acc = initial;
  for(int i = 0; i<n; i++) {
    acc += (int32_t)buf[i];
  }
  return acc;
}

// CONVENTIONS:
//
// "linear cursors" - the functions that write to buffers here take a
//  pointer into the buffer, write a variable amount of data, and
//  return the advanced cursor in the buffer.

// Write-to-memory utilities
// ------------------------------

// FIXME -- all write_* functions need to take a BOUND to avoid buffer
// overflows, *OR* we instead need to create an "infinite" buffer and
// set up a guard page.


void* amb_write_incoming_rpc(void* buf, int32_t methodID, char fireForget, void* args, int argsLen) {
  char* cursor = (char*)buf;
  int methodIDSz = zigzag_int_size(methodID);
  int totalSize = 1/*type*/ + 1/*resrvd*/ + methodIDSz + 1/*fireforget*/ + argsLen; 
  // amb_debug_log(" ... encoding incoming RPC, writing varint size %d for argsLen %d (methodID takes up %d)\n", totalSize, argsLen, methodIDSz);
  cursor = write_zigzag_int(cursor, totalSize); // Size (message header)
  *cursor++ = RPC;                            // Type (message header)
  *cursor++ = 0;                              // Reserved zero byte. 
  cursor = write_zigzag_int(cursor, methodID);  // MethodID
  *cursor++ = 1;                              // Fire and forget = 1
  memcpy(cursor, args, argsLen);              // Arguments packed tightly.
  cursor += argsLen;
  return (void*)cursor;
}

void* amb_write_outgoing_rpc_hdr(void* buf, char* dest, int32_t destLen, char RPC_or_RetVal,
			     int32_t methodID, char fireForget, int argsLen) {
  char* cursor = (char*)buf;
  int totalSize = 1 // type tag
    + zigzag_int_size(destLen) + destLen + 1 // RPC_or_RetVal
    + zigzag_int_size(methodID) + 1 // fireForget
    + argsLen;  
  cursor = write_zigzag_int(cursor, totalSize); // Size (message header)
  *cursor++ = RPC;                            // Type (message header)
  cursor = write_zigzag_int(cursor, destLen);   // Destination string size 
  memcpy(cursor, dest, destLen); cursor += destLen; // Registered name of dest service
  *cursor++ = RPC_or_RetVal;                        // 1 byte 
  cursor = write_zigzag_int(cursor, methodID);        // 1-5 bytes
  *cursor++ = fireForget;                           // 1 byte
  return (void*)cursor;
}

void* amb_write_outgoing_rpc(void* buf, char* dest, int32_t destLen, char RPC_or_RetVal,
			  int32_t methodID, char fireForget, void* args, int argsLen) {
  char* cursor = amb_write_outgoing_rpc_hdr(buf, dest, destLen, RPC_or_RetVal, methodID, fireForget, argsLen);
  memcpy(cursor, args, argsLen);                    // N bytes - Arguments packed tightly.
  cursor += argsLen;
  return (void*)cursor;
}


// This is a convenience method that sits above the buffer API:
// ------------------------------------------------------------

/*
// Logically the same as send_outgoing_*, except uses the global buffer.
// PRECONDITION: buffer is free / no outstanding "reserve" that needs to be released.
void buffer_outgoing_rpc_hdr(char* dest, int32_t destLen, char RPC_or_RetVal,
                             int32_t methodID, char fireForget, int argsLen) {  
  // Overestimate the space needed:
  int sizeBound = (1 // type tag
		   + 5 + destLen + 1  // RPC_or_RetVal
		   + 5 + 1 // fireForget
		   + 5);
  char* start = reserve_buffer(sizeBound);
  char* end = amb_write_outgoing_rpc_hdr(start, dest,destLen,RPC_or_RetVal,methodID,fireForget,argsLen);
  // If we want to create RPCBatch messages we need to only send complete messages, not just headers:
  finished_reserve_buffer(end-start);
}
*/

// Direct socket sends/recvs
// ------------------------------

void amb_send_outgoing_rpc(void* tempbuf, char* dest, int32_t destLen, char RPC_or_RetVal,
   		       int32_t methodID, char fireForget, void* args, int argsLen) {
  char* cursor0 = (char*)tempbuf;
  char* cursor = cursor0;
  int totalSize = 1 // type tag
    + zigzag_int_size(destLen) + destLen + 1 // RPC_or_RetVal
    + zigzag_int_size(methodID) + 1 // fireForget
    + argsLen;  
  cursor = write_zigzag_int(cursor, totalSize); // Size (message header)
  *cursor++ = RPC;                            // Type (message header)
  cursor = write_zigzag_int(cursor, destLen);   // Destination string size 
  memcpy(cursor, dest, destLen); cursor += destLen; // Registered name of dest service
  *cursor++ = RPC_or_RetVal;                        // 1 byte 
  cursor = write_zigzag_int(cursor, methodID);        // 1-5 bytes
  *cursor++ = fireForget;                           // 1 byte

  // This version makes even *more* syscalls, but it doesn't copy:
  socket_send_all(g_to_immortal_coord, tempbuf, cursor-cursor0, 0);
  socket_send_all(g_to_immortal_coord, args, argsLen, 0);  
  return;
}

void amb_recv_log_hdr(int sockfd, struct log_hdr* hdr) {
  // This version uses MSG_WAITALL to read in one go:
  int num = recv(sockfd, (char*)hdr, AMBROSIA_HEADERSIZE, MSG_WAITALL);
  if(num < AMBROSIA_HEADERSIZE) {
    char* err = amb_get_error_string();
    if (num >= 0) {
      fprintf(stderr,"\nERROR: connection interrupted. Did not receive all %d bytes of log header, only %d:\n  ",
	     AMBROSIA_HEADERSIZE, num);
      print_hex_bytes(amb_dbg_fd,(char*)hdr, num); fprintf(amb_dbg_fd,"\n");
    }
    fprintf(stderr,"\nERROR: failed recv (logheader), which left errno = %s\n", err);
    abort();
  }
  amb_debug_log("Read log header: { commit %d, sz %d, checksum %lld, seqid %lld }\n",
		hdr->commitID, hdr->totalSize, hdr->checksum, hdr->seqID );
  // printf("Hex: "); print_hex_bytes((char*)hdr,AMBROSIA_HEADERSIZE); printf("\n");  
  return;
}


// ------------------------------------------------------------

void attach_if_needed(char* dest, int destLen) {
  // HACK: only working for one dest atm...
  if (!g_attached && destLen != 0) // If destName=="" we are sending to OURSELF and don't need attach.
  {
      amb_debug_log("Sending attach message re: dest = %s...\n", dest);
      char sendbuf[128];
      char* cur = sendbuf;
      int dest_len = strlen(dest);
      cur = (char*)write_zigzag_int(cur, dest_len + 1); // Size
      *cur++ = (char)AttachTo;                        // Type
      memcpy(cur, dest, dest_len); cur+=dest_len;
#ifdef AMBCLIENT_DEBUG
      amb_debug_log("  Attach message: ", dest);
      print_hex_bytes(amb_dbg_fd, sendbuf, cur-sendbuf);
      fprintf(amb_dbg_fd,"\n");
#endif
      socket_send_all(g_to_immortal_coord, sendbuf, cur-sendbuf, 0);
      g_attached = 1;
      amb_debug_log("  attach message sent (%d bytes)\n", cur-sendbuf);
  }
}

/*
// INEFFICIENT version that makes an extra copy:
void send_message(char* buf, int len) {
  attach_if_needed(destName, ??); // Hard-coded global dest name.

  // FIXME - LAME COPY to PREPEND header bytes!
  char* sendbuf = (char*)malloc(1 + 5 + destLen + 1 + 5 + 1 + len);
  char* newpos = amb_write_outgoing_rpc(sendbuf, destName, destLen, 0, TPUT_MSG_ID, 1, buf, len);

  // FIXME: one system call per message!
  socket_send_all(g_to_immortal_coord, sendbuf, newpos-sendbuf, 0);
#ifdef AMBCLIENT_DEBUG
  amb_debug_log("Sent %d byte message up to coordinator, argsLen %d...\n  Hex: ", newpos-sendbuf, len);  
  print_hex_bytes(amb_dbg_fd, sendbuf, newpos-sendbuf);
  fprintf(amb_dbg_fd,"\n   Decimal: ");
  print_decimal_bytes(sendbuf, newpos-sendbuf);  printf("\n");
#endif
  free(sendbuf);
}
*/




// Begin connect_sockets:
// --------------------------------------------------
#ifdef _WIN32
void enable_fast_loopback(SOCKET sock) {
  int OptionValue = 1;
  DWORD NumberOfBytesReturned = 0;    
  int status = WSAIoctl(sock, SIO_LOOPBACK_FAST_PATH,
			&OptionValue,
			sizeof(OptionValue),
			NULL,
			0,
			&NumberOfBytesReturned,
			0,
			0);
    
  if (SOCKET_ERROR == status) {
      DWORD LastError = WSAGetLastError();
      if (WSAEOPNOTSUPP == LastError) {
	printf("WARNING: this platform doesn't support the fast loopback (needs Windows Server >= 2012).\n");
      }
      else {
	fprintf(stderr,	"\nERROR: Loopback Fastpath WSAIoctl failed with code: %d", 
		LastError);
	abort();
      }
  }
}

void connect_sockets(int* upptr, int* downptr) {
  WSADATA wsa;
  SOCKET sock;

#ifdef IPV4
  int af_inet = AF_INET;
  //  struct hostent* immortalCoord;
  //  struct sockaddr_in addr;
#else   
  int af_inet = AF_INET6;
  //  struct sockaddr_in6 addr;
#endif
   
  amb_debug_log("Initializing Winsock...\n");
  if (WSAStartup(MAKEWORD(2,2),&wsa) != 0) {
    fprintf(stderr,"\nERROR: Error Code : %d", WSAGetLastError());
    abort();
  }

  amb_debug_log("Creating to-AMBROSIA connection\n");  
  if((sock = socket(af_inet, SOCK_STREAM , 0 )) == INVALID_SOCKET) {
    fprintf(stderr, "ERROR: Could not create socket : %d" , WSAGetLastError());
    abort();
  }

  printf(" *** Configuring socket for Windows fast-loopback (pre-connect).\n");
  enable_fast_loopback(sock);
  
#ifdef IPV4     
  struct sockaddr_in addr;
  addr.sin_addr.s_addr = inet_addr(coordinator_host);
  addr.sin_family = AF_INET;
  addr.sin_port = htons( upport );
  
  if (connect(sock, (struct sockaddr *)&addr , sizeof(addr)) < 0) {
    fprintf(stderr, "\nERROR: Failed to connect to-socket: %s:%d\n", coordinator_host, upport); 
    abort();
  }
#else
  struct sockaddr_in6 addr;  
  inet_pton(AF_INET6, coordinator_host, &addr.sin6_addr);
  addr.sin6_family = af_inet;  
  addr.sin6_port = htons(upport);
  
  if (connect(sock, (struct sockaddr*)&addr, sizeof(addr)) < 0) {
    fprintf(stderr, "\nERROR: Failed to connect to-socket (ipv6): %s:%d\n Error: %s",
	    coordinator_host, upport, amb_get_error_string()); 
    abort();
  }
  /*  
    DWORD ipv6only = 0;
    if (SOCKET_ERROR == setsockopt(sock, IPPROTO_IPV6,
				   IPV6_V6ONLY, (char*)&ipv6only, sizeof(ipv6only) )) {
      fprintf(stderr, "\nERROR: Failed to setsockopt.\n"); 
      closesocket(sock);
      abort();
    }
    // Output parameters:
    SOCKADDR_STORAGE LocalAddr = {0};
    SOCKADDR_STORAGE RemoteAddr = {0};
    DWORD dwLocalAddr = sizeof(LocalAddr);
    DWORD dwRemoteAddr = sizeof(RemoteAddr);
    char upportstr[16];
    sprintf(upportstr, "%d", upport);
    if (! WSAConnectByName(sock,
			   host, 
			   upportstr,
			   &dwLocalAddr,
			  (SOCKADDR*)&LocalAddr,
			  &dwRemoteAddr,
			  (SOCKADDR*)&RemoteAddr,
			  NULL,
			  NULL) ) {
      fprintf(stderr, "\nERROR: Failed to connect (IPV6) to-socket: %s:%d\n Error: %s\n",
	      host, upport, amb_get_error_string());
      abort();
    }
  */
#endif
  // enable_fast_loopback(sock); // TEMP HACK  
  *upptr = sock;
  
  // Down link from the coordinator (recv channel)
  // --------------------------------------------------
  amb_debug_log("Creating from-AMBROSIA connection\n");
  SOCKET tempsock;
  if ((tempsock = socket(af_inet, SOCK_STREAM, 0)) == INVALID_SOCKET) {
    fprintf(stderr, "\nERROR: Failed to create (recv) socket: %d\n", WSAGetLastError());
    abort();
  }
#ifdef IPV4  
  addr.sin_family = AF_INET;
  addr.sin_addr.s_addr = INADDR_ANY;
  addr.sin_port = htons( downport );

  printf(" *** Enable fast-loopback EARLY (pre-bind):\n");
  enable_fast_loopback(tempsock); // TEMP HACK:
  
  if( bind(tempsock, (struct sockaddr *)&addr , sizeof(addr)) == SOCKET_ERROR) {
    fprintf(stderr,"\nERROR: bind returned error, addr:port is %s:%d\n Error was: %d\n",
	    coordinator_host, downport, WSAGetLastError());
    abort();
  }

  // enable_fast_loopback(tempsock); // TEMP HACK:
  
  if ( listen(tempsock,5) == SOCKET_ERROR) {
    fprintf(stderr, "ERROR: listen() failed with error: %d\n", WSAGetLastError() );
    closesocket(tempsock);
    WSACleanup();
    abort();
  }
  struct sockaddr_in clientaddr;
  int addrlen = sizeof(struct sockaddr_in);

  // enable_fast_loopback(tempsock); // Apply to the socket before accepting requests.
  
  SOCKET new_socket = accept(tempsock, (struct sockaddr *)&clientaddr, &addrlen);
  if (new_socket == INVALID_SOCKET) {
    fprintf(stderr, "ERROR: accept failed with error code : %d" , WSAGetLastError());
    abort();
  }

  // enable_fast_loopback(new_socket); // TEMP HACK:
  
#else
  // struct sockaddr_in6 addr;
  addr.sin6_family       = af_inet;
  addr.sin6_addr         = in6addr_any;
  addr.sin6_port         = htons(downport);

  if ( bind(tempsock, (SOCKADDR *) &addr, sizeof(SOCKADDR)) == SOCKET_ERROR)
  // if ( bind(tempsock, &addr, sizeof(sockaddr_in6)) == SOCKET_ERROR)
  {
    fprintf(stderr,"\nERROR: bind() failed with error when connecting to addr:port %s:%d: %s\n",
	    coordinator_host, downport, amb_get_error_string() );
    closesocket(tempsock);
    WSACleanup();
    abort();
  }
  if ( listen(tempsock, 5) == SOCKET_ERROR) {
    fprintf(stderr, "ERROR: listen() failed with error: %s\n", amb_get_error_string() );
    closesocket(tempsock);
    WSACleanup();
    abort();
  }
  SOCKET new_socket = WSAAccept(tempsock, NULL, NULL, NULL, (DWORD_PTR)NULL);
#endif
  amb_debug_log("Connection accepted from reliability coordinator\n");
  *downptr = new_socket;
  return;
}

#else
// Non-windows version:
// ------------------------------------------------------------

// Establish both connections with the reliability coordinator.
// Takes two output parameters where it will write the resulting sockets.
void connect_sockets(int* upptr, int* downptr) {
#ifdef IPV4
  struct hostent* immortalCoord;
  struct sockaddr_in addr;
  int af_inet = AF_INET;
#else   
  struct sockaddr_in6 addr;
  int af_inet = AF_INET6;
#endif
  
  // Link up to the coordinator (send channel)
  // --------------------------------------------------
  memset((char*) &addr, 0, sizeof(addr));
  amb_debug_log("Creating to-AMBROSIA connection\n");  
  if ((*upptr = socket(af_inet, SOCK_STREAM, 0)) < 0) {
    fprintf(stderr, "\nERROR: Failed to create (send) socket.\n");
    abort();
  }
#ifdef IPV4  
  immortalCoord = gethostbyname(coordinator_host);
  if (immortalCoord == NULL) {
    amb_debug_log("\nERROR: could not resolve host: %s\n", coordinator_host);
    abort();
  }
  addr.sin_family = af_inet;  
  memcpy( (char*)&addr.sin_addr.s_addr,
          (char*)(immortalCoord->h_addr_list[0]),
          immortalCoord->h_length );
  //  inet_pton(AF_INET, coordinator_host, &addr.sin_addr);  
  addr.sin_port = htons(upport);
#else
  inet_pton(AF_INET6, coordinator_host, &addr.sin6_addr);
  addr.sin6_family = af_inet;  
  addr.sin6_port = htons(upport);
#endif

  if (connect(*upptr, (struct sockaddr*)&addr, sizeof(addr)) < 0) {
    fprintf(stderr, "\nERROR: Failed to connect to-socket: %s:%d\n", coordinator_host, upport); 
    abort();
  }

  // Down link from the coordinator (recv channel)
  // --------------------------------------------------
  amb_debug_log("Creating from-AMBROSIA connection\n");
  int tempfd;
  if ((tempfd = socket(af_inet, SOCK_STREAM, 0)) < 0) {
    fprintf(stderr, "\nERROR: Failed to create (recv) socket.\n");
    abort();
  }
  memset((char*) &addr, 0, sizeof(addr));
#ifdef IPV4
  addr.sin_family       = af_inet;
  addr.sin_addr.s_addr  = INADDR_ANY;    
  addr.sin_port         = htons(downport);
#else
  addr.sin6_family       = af_inet;
  addr.sin6_addr         = in6addr_any;
  addr.sin6_port         = htons(downport);
#endif
  if (bind(tempfd, (struct sockaddr *) &addr, sizeof(addr)) < 0) {
    fprintf(stderr,"\nERROR: bind returned error, addr:port is %s:%d\n ERRNO was: %s\n",
	    coordinator_host, downport, strerror(errno));
    abort();
  }

  if ( listen(tempfd,5) ) {
    fprintf(stderr,"\nERROR: listen returned error, addr:port is %s:%d\n ERRNO was: %s\n",
	    coordinator_host, downport, strerror(errno));
    abort();
  }
#ifdef IPV4  
  struct sockaddr_in clientaddr;
#else
  struct sockaddr_in6 clientaddr;
#endif

  socklen_t addrlen = 0;
  if ((*downptr = accept(tempfd, (struct sockaddr*) &clientaddr, &addrlen)) < 0) {
    fprintf(stderr, "failed to accept connection, accept returned: %d", *downptr);
    abort();
  }
  return;
}
#endif
// End connect_sockets


// (Runtime library) Startup.
//------------------------------------------------------------------------------


// FIXME: move this to a callback argument:
extern void send_dummy_checkpoint(int upfd);


void startup_protocol(int upfd, int downfd) {
  struct log_hdr hdr; memset((void*) &hdr, 0, AMBROSIA_HEADERSIZE);
  assert(sizeof(struct log_hdr) == AMBROSIA_HEADERSIZE);

  amb_recv_log_hdr(downfd, &hdr);
  int payloadSz = hdr.totalSize - AMBROSIA_HEADERSIZE;
  char* buf = (char*)malloc(payloadSz);
  memset(buf, 0, payloadSz);

  amb_debug_log("  Log header received, now waiting on payload (%d bytes)...\n", payloadSz);
  if(recv(downfd, buf, payloadSz, MSG_WAITALL) < payloadSz) {
    fprintf(stderr,"\nERROR: connection interrupted. Did not receive all %d bytes of payload following header.",
	    payloadSz);
    abort();
  }

#ifdef AMBCLIENT_DEBUG
  amb_debug_log("  Read %d byte payload following header: ", payloadSz);
  print_hex_bytes(amb_dbg_fd, buf, payloadSz); fprintf(amb_dbg_fd,"\n");
#endif

  int32_t msgsz = -1;
  char* buf2 = read_zigzag_int(buf, &msgsz);
  if (buf2 == NULL) {
    fprintf(stderr,"\nERROR: failed to parse zig-zag int for log record size.\n");
    abort();
  }
  char msgType = *buf2;
  amb_debug_log("  Read log record size: %d\n", msgsz);
  amb_debug_log("  Read message type: %d\n", msgType);

  switch(msgType) {
  case TakeBecomingPrimaryCheckpoint:
    amb_debug_log("Starting up for the first time (TakeBecomingPrimaryCheckpoint)\n");
    break;
  case Checkpoint:
    fprintf(stderr, "RECOVER mode ... not implemented yet.\n");
    
    abort();
    break;
  default:
    fprintf(stderr, "Protocol violation, did not expect this initial message type from server: %d", msgType);
    abort();
    break;
  }
  
  // int32_t c1 = checksum(0,(char*)&hdr, AMBROSIA_HEADERSIZE);
  int32_t c2 = checksum(0,buf,payloadSz);
  amb_debug_log("  (FINISHME) Per-byte checksum just of the payload bytes: %d\n", c2);

  // Now we write our initial message.
  char msgbuf[1024];
  char argsbuf[1024];  
  memset(msgbuf, 0, sizeof(msgbuf));
  memset(argsbuf, 0, sizeof(argsbuf));
  memset(buf, 0, sizeof(buf));  

  // Temp variables:
  int32_t msgsize;
  char *msgbufcur, *bufcur;


// FIXME!! Factor this out into the client application:
#define STARTUP_ID 32
  
  // Send InitialMessage
  // ----------------------------------------
  // Here zigZagEncoding is a disadvantage, because we can't write the
  // size until we have already serialized the message, which implies a copy.  
  // It would be nice to have an encoding that could OPTIONALLY take up 5 bytes, even if
  // its numeric value doesn't mandate it.
  argsbuf[0] = 5;
  argsbuf[1] = 4;
  argsbuf[2] = 3;
  msgbufcur = amb_write_incoming_rpc(msgbuf, STARTUP_ID, 1, argsbuf, 3);
  msgsize   = msgbufcur - msgbuf;  
  // msgsize = sprintf(msgbuf, "hi");

  // Here the "+ 1" accounts for the type byte as well as the message
  // itself (data payload):
  bufcur    = write_zigzag_int(buf, msgsize + 1); // Size (w/type)
  *bufcur++ = InitialMessage;                   // Type
  memcpy(bufcur, msgbuf, msgsize);              // Lame copy!

  int totalbytes = msgsize + (bufcur-buf);
  amb_debug_log("  Now will send InitialMessage to ImmortalCoordinator, %lld total bytes, %d in payload.\n",
	 (int64_t)totalbytes, msgsize);
#ifdef AMBCLIENT_DEBUG
  amb_debug_log("  Message: ");
  print_hex_bytes(amb_dbg_fd, buf, msgsize + (bufcur-buf));
  fprintf(amb_dbg_fd,"\n");
#endif
  socket_send_all(upfd, buf, totalbytes, 0);
  /* for(int i=0; i<totalbytes; i++) {
    printf("Sending byte[%d] = %x when you press enter...", i, buf[i]);
    getc(stdin);
    socket_send_all(upfd, buf+i, 1, 0);
    } */ 
  
  // Send Checkpoint message
  // ----------------------------------------
  send_dummy_checkpoint(upfd);

  return;
}

