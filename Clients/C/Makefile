
# Put your -D variables here:
DEFINES ?= 

EXTRA_DEFINES = -DIPV4
# ^ TODO build everything twice for IPV6 vs IPV4.
# TODOTODO fix it so that one compile can work for both.

ALL_DEFINES = $(DEFINES) $(EXTRA_DEFINES)

GNULIBS= -lpthread
GNUOPTS= -pthread -O0 -g

HEADERS= include/ambrosia/internal/spsc_rring.h include/ambrosia/client.h include/ambrosia/internal/bits.h

SRCS= src/spsc_rring.c src/ambrosia_client.c
OBJS1= $(patsubst src/%.c,bin/static/%.o, $(SRCS) )

OBJS2= $(patsubst src/%.c,bin/shared/%.o, $(SRCS) )

COMP= gcc $(ALL_DEFINES) -I include/ $(GNUOPTS)
LINK= gcc $(GNULIBS)

LIBNAME=libambrosia

all: bin/$(LIBNAME).a bin/$(LIBNAME).so bin/native_hello.exe

debug:
	$(MAKE) DEFINES="-DAMBCLIENT_DEBUG" clean publish

bin/native_hello.exe: native_hello.c $(OBJS1) $(HEADERS)
	$(COMP) -c $< -o bin/static/hello.o
	$(LINK) $(OBJS1) bin/static/hello.o -o $@

bin/$(LIBNAME).a: $(OBJS1)
	ar rcs $@ $(OBJS1)

bin/$(LIBNAME).so: $(OBJS2)
	$(LINK) -shared -o $@ $(OBJS2)

bin/static/%.o: src/%.c $(HEADERS) ./bin/static
	$(COMP) -c $< -o $@

bin/shared/%.o: src/%.c $(HEADERS) ./bin/shared
	$(COMP) -fPIC -c $< -o $@

bin/static:
	mkdir -p bin/static
bin/shared:
	mkdir -p bin/shared

objclean:
	rm -rf bin

# Copy to the head of the working copy.
publish: all
	rm -rf ../../bin/include ../../bin/libambrosia.*
	mkdir -p ../../bin/include
	cp -a bin/libambrosia.*  ../../bin/
	cp -a include            ../../bin/

clean: objclean
	rm -f \#* .\#* *~

.PHONY: lin clean objclean publish
