
# Put your -D variables here, e.g. -DDEBUG
DEFINES= 

GNULIBS= -lpthread
GNUOPTS= -pthread -O3
# -std=c11

HEADERS= include/ambrosia/internal/spsc_rring.h include/ambrosia/client.h include/ambrosia/internal/bits.h

SRCS= src/spsc_rring.c src/ambrosia_client.c
OBJS1= $(patsubst src/%.c,bin/static/%.o, $(SRCS) )

OBJS2= $(patsubst src/%.c,bin/shared/%.o, $(SRCS) )

COMP= gcc $(DEFINES) -I include/ $(GNUOPTS)
LINK= gcc

LIBNAME=libambrosia

all: bin/$(LIBNAME).a bin/$(LIBNAME).so 

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
