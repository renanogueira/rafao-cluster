# Solução de problemas

## O worker não responde

- confirme se o worker está iniciado;
- confira IP e porta no log;
- teste `ping` e a porta `50052`;
- verifique se as máquinas estão na mesma sub-rede;
- revise o Firewall do Windows, `ufw` ou o firewall do macOS;
- não use o IP de VPN, Docker ou interface virtual.

PowerShell:

```powershell
Test-NetConnection 192.168.0.11 -Port 50052
```

macOS/Linux:

```bash
nc -vz 192.168.0.11 50052
```

## RPC server version mismatch

Esse erro indica que coordenador e worker executam revisões diferentes do
`llama.cpp`, mesmo que a porta TCP responda. Pare os processos, atualize o Rafão
Cluster em todos os computadores, execute **Instalar / atualizar** em cada nó e
reinicie primeiro os workers. A versão 0.4.4 fixa todos os sistemas na build
`b12110`.

## A GPU não aparece

### Windows/Linux NVIDIA

Execute `nvidia-smi`. Se o comando não existir ou falhar, reinstale/atualize o
driver antes do `llama.cpp`.

### macOS

Use Apple Silicon e confirme que o dispositivo `Metal0` aparece no log do
worker.

## Falta memória

- reduza o contexto para `4096` ou menos;
- escolha um GGUF menor;
- use uma quantização menor;
- feche programas que estejam usando a GPU;
- confirme que todos os workers foram conectados antes do carregamento.

## A geração está lenta

- troque Wi-Fi por Ethernet;
- confira se a interface negociou 1/2.5/10 GbE corretamente;
- teste sem workers para comparar o custo da rede;
- monitore GPU, CPU, RAM e tráfego durante a geração;
- confirme que o modelo não está fazendo offload excessivo para RAM/CPU.

## O download do GGUF falha

- confira o nome completo do repositório no Hugging Face;
- para modelos restritos, aceite a licença no site;
- forneça um token somente de leitura;
- nunca publique o token em issues ou logs;
- confirme espaço livre no disco.

## O macOS bloqueia o aplicativo

Use botão direito → **Abrir** na primeira execução. Não desative o Gatekeeper.
O pacote atual ainda não é notarizado pela Apple.

