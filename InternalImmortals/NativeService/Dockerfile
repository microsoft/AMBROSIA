FROM ambrosia

RUN apt-get install -y make gcc strace nmap
ADD . /root/NativeService
WORKDIR /root/NativeService

RUN make -f Makefile.lin service_v4.exe

# NOTE! Run with AZURE_STORAGE_CONN_STRING in the environment:
# ENV AZURE_STORAGE_CONN_STRING 
