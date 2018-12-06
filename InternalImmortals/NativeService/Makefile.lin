
DEFINES= 

GNULIBS= -lpthread
GNUOPTS= -pthread -O3
# -std=c11

HEADERS= include/spsc_rring.h include/ambrosia_client.h

# SRC = $(wildcard src/*.c)
SRCS= src/spsc_rring.c src/ambrosia_client.c
OBJS= $(patsubst src/%.c,bin/%.o, $(SRCS) )

COMP= gcc $(DEFINES) -I include/ $(GNUOPTS)
LINK= gcc

all: lin
lin: service_v4.exe service_v6.exe
dbg: service_dbg_v4.exe

# bin/spsc_rring.o: src/spsc_rring.c $(HEADERS) ./bin
bin/%.o: src/%.c $(HEADERS) ./bin
	$(COMP) -c $< -o $@

# TEMP:
service.o: service.c
	$(COMP) -c $< -o $@

service_lin.exe: service.o $(OBJS)
	$(LINK) service.o $(OBJS) $(GNULIBS) -o $@

service_v4.exe: $(HEADERS) $(SRCS) service.c
	$(MAKE) -f Makefile.lin DEFINES="-DIPV4" objclean service_lin.exe
	mv service_lin.exe $@

service_dbg_v4.exe: $(HEADERS) $(SRCS) service.c
	$(MAKE) -f Makefile.lin DEFINES="-DAMBCLIENT_DEBUG -DIPV4" objclean service_lin.exe
	mv service_lin.exe $@


service_v6.exe: $(HEADERS) $(SRCS) service.c
	$(MAKE) -f Makefile.lin DEFINES="" objclean service_lin.exe
	mv service_lin.exe $@

bin:
	mkdir bin


objclean:
	rm -rf bin

clean: objclean
	rm -f service_winsockv4.exe service_winsockv6.exe service_v4.exe service_v6.exe
	rm -f \#* .\#* *~

.PHONY: lin clean objclean
