#!/bin/bash
set -euo pipefail
echo
echo "--------------------------------------------------------------------------------"
echo "Run Hello World app process"
echo "--------------------------------------------------------------------------------"
echo
if ! [ ${PORTOFFSET:+defined} ]; then PORTOFFSET=0; fi
PORT1=$((6000 + PORTOFFSET))
PORT2=$((6001 + PORTOFFSET))
PORT3=$((7000 + PORTOFFSET))
PORT4=$((7001 + PORTOFFSET))

export AMBROSIA_IMMORTALCOORDINATOR_PORT=$((6000 + PORTOFFSET))
export AMBROSIA_INSTANCE_NAME=hello`whoami`
set -x

time Ambrosia RegisterInstance -i $AMBROSIA_INSTANCE_NAME --rp $PORT1 --sp $PORT2 -l "./logs/"
time Ambrosia RegisterInstance -i $AMBROSIA_INSTANCE_NAME --rp $PORT3 --sp $PORT4 -l "./logs/"

rm -rf logs # Delete logs and run fresh for this example.

runAmbrosiaService.sh ./bin/native_hello.exe $PORT1 $PORT2

set +x
echo "Attempt a cleanup of our table metadata:"
time UnsafeDeregisterInstance $AMBROSIA_INSTANCE_NAME || true
echo "All done."
