FROM mcr.microsoft.com/dotnet-buildtools/prereqs:centos-stream-10-amd64

ENTRYPOINT ["/usr/bin/env"]

RUN apk add git py3-pip && python3 -m pip install pandocfilters

CMD /manpages/tool/update-man-pages.sh
