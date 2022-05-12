#!/usr/bin/env bash

cd ../
docker login registry.gitlab.com
docker build -t registry.gitlab.com/darktidelegend/ace .

