#!/usr/bin/env bash
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive

apt-get update
apt-get install -y --no-install-recommends \
  ca-certificates \
  curl \
  build-essential \
  ninja-build \
  python3.12 \
  python3.12-dev \
  python3.12-venv

curl --fail --location \
  --output /tmp/cuda-keyring.deb \
  https://developer.download.nvidia.com/compute/cuda/repos/wsl-ubuntu/x86_64/cuda-keyring_1.1-1_all.deb
dpkg -i /tmp/cuda-keyring.deb
apt-get update
apt-get install -y --no-install-recommends \
  cuda-compiler-12-9 \
  libcurand-dev-12-9

python3.12 -m venv /opt/uv-bootstrap
/opt/uv-bootstrap/bin/python -m pip install --upgrade pip uv

/opt/uv-bootstrap/bin/uv venv /opt/vllm --python python3.12
/opt/uv-bootstrap/bin/uv pip install \
  --python /opt/vllm/bin/python \
  --upgrade \
  --pre \
  --extra-index-url https://wheels.vllm.ai/nightly/cu129 \
  --extra-index-url https://download.pytorch.org/whl/cu129 \
  --index-strategy unsafe-best-match \
  vllm

install -d -m 0755 /etc/vllm /var/lib/vllm /var/cache/huggingface

curl --fail --location \
  --output /etc/vllm/tool_chat_template_gemma4.jinja \
  https://raw.githubusercontent.com/vllm-project/vllm/main/examples/tool_chat_template_gemma4.jinja

/opt/vllm/bin/vllm --version | tee /etc/vllm/vllm-version.txt
