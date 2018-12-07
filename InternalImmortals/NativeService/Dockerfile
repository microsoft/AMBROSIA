# This Dockerfile demonstrates how we don't need to have the
# "ambrosia" Docker image as our *parent* in order to use AMBROSIA.
# We can also just use it as a convenient way to grab binary releases
# of AMBROSIA.
FROM ambrosia

# We can use whatever we want as our parent image here:
FROM ubuntu
# Build dependencies for the native service:
RUN apt-get update && \
    apt-get install -y make gcc 
# Ambrosia runtime dependencies, including rotatelogs:
RUN apt-get install -y libunwind-dev libicu60 apache2-utils

# Equivalently, we could wget and extract a tarball here:
COPY --from=ambrosia /ambrosia/bin /ambrosia/bin

ADD . /ambrosia/NativeService
WORKDIR /ambrosia/NativeService

ENV AMBROSIA_BINDIR=/ambrosia/bin

# This will leave the binary in the current directory:
RUN make service_v4.exe 

ENV PATH="${PATH}:/ambrosia/bin"

# NOTE! Run this with AZURE_STORAGE_CONN_STRING in the environment:
# docker run --env AZURE_STORAGE_CONN_STRING="$AZURE_STORAGE_CONN_STRING" ...
