# Instalação por plataforma

Os pacotes publicados são autocontidos. O runtime .NET só é necessário para
compilar o projeto.

## Windows 10/11 x64

### Requisitos

- placa NVIDIA compatível com CUDA;
- driver NVIDIA recente;
- rede configurada como **Privada**;
- aproximadamente 2 GB livres para binários e espaço adicional para GGUFs.

### Instalação

1. Extraia `RafaoCluster-win-x64.zip` numa pasta permanente.
2. Execute `RafaoCluster.exe` como usuário normal.
3. Clique em **Diagnosticar** e confira a GPU e o IP.
4. Clique em **Instalar / atualizar**.

O aplicativo só pede elevação administrativa ao alterar o Firewall do Windows.
Não é necessário executá-lo permanentemente como administrador.

## macOS Apple Silicon

### Requisitos

- macOS 13 ou posterior;
- Mac com Apple Silicon;
- [Homebrew](https://brew.sh/) para instalar o `llama.cpp`.

### Instalação

1. Extraia `RafaoCluster-macos-arm64.zip`.
2. Mova o aplicativo para `/Applications`.
3. Na primeira abertura, use botão direito → **Abrir**.
4. Autorize conexões locais caso o firewall pergunte.
5. Use **Instalar / atualizar** dentro do aplicativo. Se o Mac estiver como
   worker, a primeira instalação também compilará o `ggml-rpc-server` com Metal
   e poderá levar alguns minutos.

O `llama.cpp` usa Metal por padrão no macOS. O pacote ainda não é notarizado;
não desative o Gatekeeper para contornar a primeira abertura.

## Linux x64

### Dependências de interface

Ubuntu/Debian:

```bash
sudo apt update
sudo apt install -y libx11-6 libice6 libsm6 libfontconfig1
```

### Instalação

```bash
mkdir -p "$HOME/Applications/RafaoCluster"
tar -xzf RafaoCluster-linux-x64.tar.gz \
  -C "$HOME/Applications/RafaoCluster"
chmod +x "$HOME/Applications/RafaoCluster/RafaoCluster"
"$HOME/Applications/RafaoCluster/RafaoCluster"
```

Com `nvidia-smi` disponível, o instalador busca automaticamente o release CUDA
12 do `llama.cpp`. Sem uma GPU NVIDIA detectada, usa o release CPU.

### Firewall Linux

Permita o RPC apenas a partir do coordenador:

```bash
sudo ufw allow from 192.168.0.10 to any port 50052 proto tcp
```

Troque `192.168.0.10` pelo IP real do coordenador.

## Pastas de dados

| Plataforma | Configuração e dados locais |
|---|---|
| Windows | `%LOCALAPPDATA%\RafaoCluster` |
| macOS | `~/Library/Application Support/RafaoCluster` |
| Linux | `~/.local/share/RafaoCluster` |

Modelos GGUF podem ocupar dezenas ou centenas de gigabytes. Escolha um disco
com espaço e desempenho adequados.

