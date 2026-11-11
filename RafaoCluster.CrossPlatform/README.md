# Rafao Cluster 0.4.6 — macOS e Linux

Interface dark para configurar um coordenador `llama-server` e workers RPC do
llama.cpp na mesma rede local. O aplicativo também diagnostica hardware/rede e
baixa modelos GGUF do Hugging Face.

O downloader inclui um catálogo de modelos públicos e gratuitos, ordenado do
menor para o maior, com tamanho Q4 estimado, finalidade e licença. O campo de
repositório permanece editável para downloads manuais.

O painel de execução acompanha conexão RPC, dispositivo, buffers remotos e
grafos processados. Quando a contagem aumenta, o worker está participando da
inferência; não é necessário acompanhar outro terminal.

## macOS (Apple Silicon)

1. Descompacte `RafaoCluster-macos-arm64.zip`.
2. Arraste `RafaoCluster.app` para Aplicativos.
3. Na primeira abertura, clique com o botão direito no app e escolha **Abrir**.
4. Instale o Homebrew, se ainda não existir. O botão **Instalar / atualizar
   llama.cpp** instala as dependências e compila `llama-server` e
   `ggml-rpc-server` com Metal e RPC habilitados. Esse passo é necessário porque
   a fórmula do Homebrew não oferece um backend RPC utilizável pelo cluster.

Este pacote é assinado localmente, mas não é notarizado com uma conta Apple
Developer. Nunca desative o Gatekeeper para abri-lo.

## Linux x64

Em Ubuntu/Debian com ambiente gráfico:

```bash
sudo apt update
sudo apt install -y libx11-6 libice6 libsm6 libfontconfig1
tar -xzf RafaoCluster-linux-x64.tar.gz -C RafaoCluster
chmod +x RafaoCluster/RafaoCluster
./RafaoCluster/RafaoCluster
```

Para NVIDIA, instale antes um driver que forneça `nvidia-smi`. O botão de
instalação detecta isso e baixa automaticamente o release CUDA 12 oficial do
llama.cpp; sem NVIDIA, baixa o release CPU.

## Primeiro teste com dois computadores

1. Confirme que ambos estão na rede `192.168.0.x` e escolha IPs estáveis.
2. No worker, escolha **Worker**, informe o IP local e a porta RPC `50052`,
   instale o llama.cpp e inicie.
3. No Linux, permita somente a rede local: `sudo ufw allow from 192.168.0.0/24
   to any port 50052 proto tcp`. No macOS, autorize o app no firewall quando
   solicitado. Não exponha essa porta na internet.
4. No coordenador, informe `IP_DO_WORKER:50052`, baixe/escolha o GGUF, mantenha
   a API em `8080` e inicie.
5. Teste no próprio coordenador:

```bash
curl http://127.0.0.1:8080/health
curl http://127.0.0.1:8080/v1/models
```

Adicione outros workers separados por vírgula. Para desempenho real, prefira
Ethernet cabeada de 2.5 GbE ou 10 GbE; Wi-Fi funciona para validar, mas vira
gargalo na inferência distribuída.
