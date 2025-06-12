set -euo pipefail

baseXmlPath="/root/local-git/MergedManifest.xml"

nodes=$(xmllint --xpath '//Blob[contains(@Id, "dotnet-sdk") and contains(@Id, "linux") and contains(@Id, "x64.tar.gz")]' "$baseXmlPath")


while read -r node; do
    name=$(echo "$node" | xmllint --xpath 'string(//@PipelineArtifactName)' -)
    path=$(echo "$node" | xmllint --xpath 'string(//@PipelineArtifactPath)' -)

    if [[ "$path" =~ dotnet-sdk-[0-9]+\.[0-9]+\.[0-9]+(-?(alpha|preview|rc|rtm)[0-9\.\-]*)?-linux.*-x64\.tar\.gz$ ]]; then
        echo "Artifact Name: $name"
        echo "Artifact Path: $path"
    fi
done <<< "$nodes"