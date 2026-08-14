# Stage 1: Build Renode from source
FROM mcr.microsoft.com/dotnet/sdk:6.0-jammy AS build

RUN apt-get update && apt-get install -y --no-install-recommends \
        automake \
        autoconf \
        libtool \
        g++ \
        libgtk2.0-dev \
        screen \
        uml-utilities \
        gtk-sharp2 \
        python3 \
        python3-pip \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /renode
COPY . .

# Initialise submodules and build
RUN git submodule update --init --recursive
RUN ./build.sh

# Stage 2: Runtime image
FROM mcr.microsoft.com/dotnet/runtime:6.0-jammy

RUN apt-get update && apt-get install -y --no-install-recommends \
        python3 \
        libgtk2.0-0 \
        screen \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /opt/renode
COPY --from=build /renode/output/bin/ ./bin/
COPY --from=build /renode/scripts/ ./scripts/
COPY --from=build /renode/platforms/ ./platforms/
COPY --from=build /renode/tests/ ./tests/

ENTRYPOINT ["dotnet", "/opt/renode/bin/Renode.dll"]
