# Sourced by CI scripts.

export AMBROSIA_ROOT=`pwd`

# Gather a bit of info about where we are:
uname -a
pwd -P
cat /etc/issue || true

if [ $# -eq 0 ]; then
    mode=nodocker
else
    mode=$1
fi

# Avoiding collisions on concurrently running tests is a difficult
# business.  Here we use the CI build ID (Azure Pipelines) if
# available, to make more unique CRA instance names.
INSTPREF=`whoami`
if [ ${BUILD_BUILDID:+defined} ]; then
    INSTPREF=${INSTPREF}"$BUILD_BUILDID"
fi
export INSTPREF
