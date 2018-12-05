
Ambrosia Native Bindings (Client Code in C)
===========================================

The AMBROSIA model is language-agnostic, but like other RPC-based
systems it needs a notion of per-language bindings to make sending
AMBROSIA RPCs painless and idiomatic.

This directory contains `libambrosia`.  It is the most bare-bones
"language binding" imaginable: a basic C implementation of the
low-level wire protocols obeyed bythe AMBROSIA immortal coordinators.

libambrosia is useful for:

 * C/C++ programmers, who could perhaps use it directly (but higher
   level wrappers would be preferrable)

 * Authors of new language-bindings

Because libambrosia is a small native code library with C calling
conventions, it can be easily accessed via the foreign function
interface (FFI) on most any programming language.  This is the first
step in creating an idiomatic RPC experience in those languages,
integrating with existing serialization and RPC mechanisms.


libambrosia Linux Build
-----------------------

Simple running `make` will build a static and dynamic version of the
library using `gcc`.  There are no dependencies.

    make

The output resides in `bin/libambrosia.*`, also,
`include/ambrosia/client.h`.

You can also see the Dockerfile at the root of this repo, which builds
libambrosia.


libambrosia Windows Build
-------------------------

On Windows, this library builds with `cl.exe`.  You'll need to start
up a command prompt with this in your path.  If you have visual studio
installed, you may find an application in your start menu called
something like "x64 Native Tools Command Prompt for VS 2017".
Alternatively, load `vcvarsall.bat` into an existing shell, and then
run `nmake`:

    C:\...\ambrosia\Clients\C> nmake -f Makefile.win


