# set -x
vmrSrcDir="/root/local-git/src"
# Builds an Azure DevOps matrix definition. Each entry in the matrix is a path,
# allowing a job to be run for each src repo.
matrix=""
count=0

# Trim leading/trailing spaces from the repo name
repo=" "

specificRepoName=$(echo "$repo" | awk '{$1=$1};1')

get_type(){
    local var_name="$1"
    if  [[ "$(declare -p $var_name1 2>/dev/null)" =~ "declare -a" ]]; then
        echo "Array"
    elif  [[ "$(declare -p $var_name 2>/dev/null)" =~ "declare -A" ]]; then
        echo "Associative Array"
    elif  [[ "$(declare -p $var_name 2>/dev/null)" =~ "declare -i" ]]; then
        echo "Integer"
    elif  [[ "$(declare -p $var_name 2>/dev/null)" =~ "readonly" ]]; then
        echo "Read-only String"
    elif  [[ -v $var_name ]]; then
        echo "String"
    else
        echo "Undefined"
    fi
}

# If the repo name is provided, only scan that repo.
if [ ! -z "$specificRepoName" ]; then
    matrix="\"$specificRepoName\": { \"repoPath\": \"$vmrSrcDir/$specificRepoName\" }"
else
    for dir in $vmrSrcDir/*/
    do
    if [ ! -z "$matrix" ]; then
        matrix="$matrix,"
    fi
    repoName=$(basename $dir)
    matrix="$matrix \"$repoName\": { \"repoPath\": \"$dir\" }"
    if [ "$count" != "1" ]; then
        count=$((count+1))
    else
        break
    fi
    done
fi

matrix="{ $matrix }"

# echo "##vso[task.setvariable variable=repoMatrix;isOutput=true]$matrix"

# # Remove the quotes from the repo name
# mm=$(echo "$matrix" | tr -d '"')
# echo "Repo1: $mm"
# mm=$(echo "$mm" | grep -oP '\b\w+(?=: { repoPath:)')
# echo "Repo2: $mm"
# get_type "$mm"


mm=$(echo "$matrix" | jq -r 'keys | join(",")')
echo $mm
declare -p mm
$(declare -p mm 2>/dev/null)
get_type $mm