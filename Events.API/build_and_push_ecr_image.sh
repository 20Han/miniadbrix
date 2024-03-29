﻿#!/bin/bash
set -e

aws ecr get-login-password --region ap-northeast-3 --profile user1 | docker login --username AWS --password-stdin 524534496476.dkr.ecr.ap-northeast-3.amazonaws.com
docker build  --platform=linux/amd64 -t mini_adbrix_repository .
docker tag mini_adbrix_repository:latest 524534496476.dkr.ecr.ap-northeast-3.amazonaws.com/mini_adbrix_repository:latest
docker push 524534496476.dkr.ecr.ap-northeast-3.amazonaws.com/mini_adbrix_repository:latest
