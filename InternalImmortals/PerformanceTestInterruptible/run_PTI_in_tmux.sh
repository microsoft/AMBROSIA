#!/bin/bash
set -xeuo pipefail

# This is a script to run PTI interactively to allow a human observer
# to see all four processes as panes of a tmux session.

# It is typically called WITHIN a docker container, like so:
#  docker run -it --rm --env AZURE_STORAGE_CONN_STRING="$AZURE_STORAGE_CONN_STRING" ambrosia-perftest  ./run_PTI_in_tmux.sh

source `dirname $0`/Scripts/default_var_settings.sh

export PATH="$PATH:/ambrosia/bin"

Ambrosia RegisterInstance -i $CLIENTNAME --rp $PORT1 --sp $PORT2 -l "/ambrosia_logs/" 
Ambrosia RegisterInstance -i $SERVERNAME --rp $PORT3 --sp $PORT4 -l "/ambrosia_logs/" 

# We could start the process directly in each pane, but starting a
# terminal and then sending the keys leaves each pane open after an
# error to see the message:
tmux new-session  -d \; \
     send-keys "/ambrosia/bin/ImmortalCoordinator -i dockertest2 -p $CRAPORT2" C-m ;
tmux split-window -v \; \
     send-keys "/ambrosia/bin/ImmortalCoordinator -i dockertest1 -p $CRAPORT1" C-m ;
tmux split-window -h \; \
     send-keys "sleep 10; /ambrosia/bin/Job --rp $PORT2 --sp $PORT1 -j dockertest1 -s dockertest2 --mms 65536 -n 9999 -c" C-m ; 
tmux select-pane -t 0 \; \
     split-window -h  \; \
     send-keys "sleep 10; /ambrosia/bin/Server --rp $PORT4 --sp $PORT3 -j dockertest1 -s dockertest2 -n 1 -c" C-m ;
tmux attach
