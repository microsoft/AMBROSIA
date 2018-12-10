// Small helpers and potentially reusable bits.


// Internal helper: try repeatedly on a socket until all bytes are sent.
static inline void socket_send_all(int sock, const void* buf, size_t len, int flags) {
  char* cur = (char*)buf;
  int remaining = len;
  while (remaining > 0) {
    int n = send(sock, cur, remaining, flags);
    if (n < 0) {
      char* err = amb_get_error_string();
      fprintf(stderr,"\nERROR: failed send (%d bytes, of %d) which left errno = %s\n",
	      remaining, (int)len, err);
      abort();
    }
    cur += n;
    remaining -= n;
#ifdef AMBCLIENT_DEBUG
    if (remaining > 0)
      amb_debug_log(" Warning: socket send didn't get all bytes across (%d of %d), retrying.\n", n, remaining);
#endif
  }
}
