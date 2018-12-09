#!/bin/bash

echo "This test will look ugly, but it is a way to make sure that the interactive demo doesn't become rotted."
echo 
echo "Launching docker image then sleeping:"
rm -f cont.id
docker run -t --rm --cidfile cont.id \
       --env AZURE_STORAGE_CONN_STRING="$AZURE_STORAGE_CONN_STRING" ambrosia-perftest \
       ./run_PTI_in_tmux.sh &

TIME=25
sleep $TIME
clear
echo "$TIME seconds later, do things seem ok?"
docker ps

echo "Checking the output of 'ps auxww' in the container..."
docker exec $(docker ps -q) ps auxww  2>&1 > ps.out
docker kill $(cat cont.id)

echo "Basic health check: were all four processes still alive?"
hits1=`grep /ambrosia/bin/ImmortalCoordinator ps.out | grep -v tmux | wc -l`
hits2=`grep bin/Job ps.out | grep -v tmux | wc -l`
hits3=`grep bin/Server ps.out | grep -v tmux | wc -l`

if [ $hits1 != 2 ] || [ $hits2 != 1 ] || [ $hits3 != 1 ]; then 
    echo "Failed health check."
    echo "Processes were: "
    cat ps.out
    exit 1
else 
    echo "Passed health check."
    rm ps.out cont.id || true
    exit 0
fi




