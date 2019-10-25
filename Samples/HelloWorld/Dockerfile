
# ------------------------------
FROM ambrosia/ambrosia as amb
FROM microsoft/dotnet:2.2-sdk
COPY --from=amb /ambrosia/bin /ambrosia/bin
ENV PATH="$PATH:/ambrosia/bin"
# ------------------------------

ADD . /src
WORKDIR /src
RUN ./build_dotnetcore.sh
