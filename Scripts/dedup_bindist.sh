#!/bin/bash
set -euo pipefail

# Our build consists of separate "dotnet publish" commands that are
# directed to separate subdirectories of the output.

primary="runtime"
secondary=" coord codegen unsafedereg "

cd `dirname $0`/../bin
bindir=`pwd -P`
echo "Total files in bin/: "`find . | wc -l`

for dir in $secondary; do
    echo
    echo "Processing $dir"
    cd "$bindir/$dir"
    echo "  Files: "`ls | wc -l`
    dups=`mktemp /tmp/dupslist.XXXXX`
    echo "  Comparing ../$primary/ ./"
    
    diffs=`mktemp`
    diff -srq ../$primary/ ./ > $diffs || true
    echo "  Computed diffs..."
    
    # FRAGILE:
    # Expects Files __ and __ are identical
    cat $diffs | grep identical \
               | awk '{ print $4 }' \
               > $dups
    
    echo "  Found $(cat $dups | wc -l) duplicates."
    echo -ne "  Linking: "
    while read f; do        
        echo -ne "."
        # Requires realpath from GNU coreutils:
        dirof=`dirname $f`
        relative=`realpath ../runtime/$f --relative-to=$dirof`
        # echo "ln -sf $relative $f"
        ln -sf $relative $f
    done < $dups
    echo

    rm $diffs
    rm $dups
done

echo "Done with all subdirectories."

echo " |-> Looking for broken links:"
cd "$bindir"
broken=`mktemp`
find . -type l ! -exec test -e {} \; -print | tee $broken

if [ "$(cat $broken)" == "" ];
then
    echo " |-> No broken links found."
else
    echo
    echo "ERROR: found broken links."
    echo "Number of broken: $(cat $broken | wc -l)"
    echo "Sample:"
    head -n10 $broken
    exit 1
fi
