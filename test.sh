set -euo pipefail

baseXmlPath="/root/local-git-backup/MergedManifest.xml"

nodes=$(xmllint --xpath "string(//Blob[contains(@Id, 'Sdk') and contains(@Id, '-linux') and contains(@Id, '-x64.tar.gz')])" "$baseXmlPath")

while read -r node; do
    name=$(echo "$node" | xmllint --xpath "string(//@PipelineArtifactName)" -)
    path=$(echo "$node" | xmllint --xpath "string(//@PipelineArtifactPath)" -)

    echo "Artifact Name: $name"
    echo "Artifact Path: $path"
done <<< "$nodes"