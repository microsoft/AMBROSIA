# ------------------------------------------------------------------------------
# This Makefile builds multiple versions of the "service" binary:
# debug/release and ipv4/ipv6.
#
# ASSUMES:
# (1) that libambrosia.a is available and published to the lib/
#     subdirectory of this AMBROSIA working copy.
# (2) that headers are likewise published to the include/ dir at
#     the top of the working copy.
# ------------------------------------------------------------------------------

# This will be set by recursive invocations of Make:
DEFINES= 

GNUOPTS= -pthread -O3
COMP= gcc -c $(DEFINES) -I ../../bin/include/ $(GNUOPTS)
LIBS= -L ../../bin -l:libambrosia.a -lpthread
LINK= gcc

all: service_v4.exe service_v6.exe
dbg: service_dbg_v4.exe

service_temp.exe: service.c
	$(COMP) service.c -o service.o
	$(LINK) service.o $(LIBS) -o $@

service_v4.exe: $(HEADERS) $(SRCS) service.c
	$(MAKE) DEFINES="-DIPV4" partclean service_temp.exe
	mv service_temp.exe $@

service_dbg_v4.exe: $(HEADERS) $(SRCS) service.c
	$(MAKE) DEFINES="-DAMBCLIENT_DEBUG -DIPV4" partclean service_temp.exe
	mv service_temp.exe $@

service_v6.exe: $(HEADERS) $(SRCS) service.c
	$(MAKE) DEFINES="" partclean service_temp.exe
	mv service_temp.exe $@

partclean:
	rm -f service_temp.exe

clean: 
	rm -f service_winsockv4.exe service_winsockv6.exe service_v4.exe service_v6.exe
	rm -f \#* .\#* *~

.PHONY: lin clean partclean
