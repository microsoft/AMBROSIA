# This Dockerfile demonstrates how we don't need to have the
# "ambrosia" Docker image as our *parent* in order to use AMBROSIA.
# We can also just use it as a convenient way to grab binary releases
# of AMBROSIA.
# FROM ambrosia
#
# # We can use whatever we want as our parent image here:
# FROM ubuntu
# # Build dependencies for the native service:
# RUN apt-get update && \
#     apt-get install -y make gcc 
# # Ambrosia runtime dependencies, including rotatelogs:
# RUN apt-get install -y libunwind-dev libicu60 apache2-utils

# # Equivalently, we could wget and extract a tarball here:
# COPY --from=ambrosia /ambrosia/bin /ambrosia/bin
# ----------------------------------------

# FROM ambrosia
# [2018.12.08] right now,  running into:
#   + Ambrosia RegisterInstance -i native1 --rp 50001 --sp 50002 -l ./ambrosia_logs/
#   Error trying to upload service. Exception: Method not found: 'Remote.Linq.Expressions.Expression Remote.Linq.ExpressionTranslator.ToRemoteLinqExpression(System.Linq.Expressions.Expression, Aqua.TypeSystem.ITypeInfoProvider, System.Func`2<System.Linq.Expressions.Expression,Boolean>)'.

# Oh, that's happening even on ambrosia-dev:
FROM ambrosia-dev
# We get build dependencies from ambrosia-dev, since it built the native library.
# RUN apt-get update && \
#     apt-get install -y make gcc
# nmap

ADD   . /ambrosia/NativeService
WORKDIR /ambrosia/NativeService

ENV AMBROSIA_BINDIR=/ambrosia/bin

# This will leave the binary in the current directory:
RUN make clean service_v4.exe 

ENV PATH="${PATH}:${AMBROSIA_BINDIR}"

# NOTE! Run this with AZURE_STORAGE_CONN_STRING in the environment:
# docker run --env AZURE_STORAGE_CONN_STRING="$AZURE_STORAGE_CONN_STRING" ...
