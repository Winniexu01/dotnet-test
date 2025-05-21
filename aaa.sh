vmrSrcDir="/root/local-git/eng/tools"

for entry in "$vmrSrcDir"/*
do
    echo "$entry"
done


find $vmrSrcDir -type f