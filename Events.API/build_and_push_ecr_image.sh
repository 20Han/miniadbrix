#!/bin/bash
set -e

aws ecr get-login-password --region ap-northeast-2 --profile user1 | docker login --username AWS --password-stdin 524534496476.dkr.ecr.ap-northeast-2.amazonaws.com
docker build -f ./Dockerfile -t cdkstack-miniadbrixrepositorybf299eb7-d14p2euxlbdf:latest .
docker tag cdkstack-miniadbrixrepositorybf299eb7-d14p2euxlbdf:latest 524534496476.dkr.ecr.ap-northeast-2.amazonaws.com/cdkstack-miniadbrixrepositorybf299eb7-d14p2euxlbdf:latest
docker push 524534496476.dkr.ecr.ap-northeast-2.amazonaws.com/cdkstack-miniadbrixrepositorybf299eb7-d14p2euxlbdf:latest
