
# ------------------------------
FROM ambrosia/ambrosia as amb
FROM mcr.microsoft.com/dotnet/core/sdk:3.1
COPY --from=amb /ambrosia/bin /ambrosia/bin
ENV PATH="$PATH:/ambrosia/bin"
# ------------------------------

ADD . /src
WORKDIR /src
RUN ./build_dotnetcore.sh
