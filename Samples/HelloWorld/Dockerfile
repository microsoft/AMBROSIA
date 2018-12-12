
# ------------------------------
# OPTION (1)
# FROM ambrosia
# FROM microsoft/dotnet:2.0-sdk
# COPY --from=ambrosia /ambrosia/bin /ambrosia/bin
# ENV PATH="$PATH:/ambrosia/bin"
# ------------------------------

# OPTION (2)
FROM ambrosia-dev
# ------------------------------

ADD . /src
WORKDIR /src
# RUN ./build_dotnetcore.sh
