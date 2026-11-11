# Rafão Cluster

Aplicativo Windows para configurar um pequeno cluster de inferência LLM baseado no RPC do `llama.cpp`.

A interface usa tema escuro integral, incluindo barra de título, menus, listas, confirmações e seletores de arquivo/pasta próprios, com todo o texto em branco.

## O que o MVP faz

- escolhe entre **Coordenador** e **Worker**;
- detecta Windows, RAM, GPUs NVIDIA, VRAM, driver, IPs e velocidade das interfaces;
- baixa uma release compatível do `llama.cpp` com CUDA 12.4;
- consulta repositórios do Hugging Face, lista arquivos GGUF e baixa a quantização escolhida com progresso;
- inicia `llama-server` no coordenador com divisão por camadas;
- testa cada worker por TCP antes de iniciar o modelo;
- inicia o servidor RPC no worker vinculado a um IP específico;
- cria, mediante confirmação do Windows, uma regra de firewall limitada ao IP do coordenador;
- mantém API do coordenador em `127.0.0.1` por padrão;
- salva a configuração em `%LOCALAPPDATA%\RafaoCluster\settings.json`.

## Gerar o EXE no Windows

Abra PowerShell nesta pasta:

```powershell
winget install Microsoft.DotNet.SDK.8
Set-ExecutionPolicy -Scope Process Bypass
.\build-windows.ps1
```

O resultado será `outputs\RafaoCluster-win-x64\RafaoCluster.exe`.

O executável publicado é autocontido: os PCs do cluster não precisam instalar o .NET Runtime.

Consulte `TUTORIAL.md` para o roteiro completo do primeiro worker até um modelo distribuído maior.

## Fluxo sugerido

1. Execute o aplicativo em cada PC.
2. Clique em **Diagnosticar** e confirme que GPU e rede dedicada aparecem.
3. Clique em **Instalar / atualizar** em todos os PCs.
4. No worker, informe o IP dedicado e o IP do coordenador.
5. Clique em **Configurar firewall** e revise a confirmação administrativa.
6. Inicie o worker.
7. No coordenador, informe `IP_DO_WORKER:50052`, escolha o GGUF e inicie.

## Segurança

O RPC do `llama.cpp` é experimental e não deve ser publicado na internet nem numa rede não confiável. Use uma NIC/VLAN dedicada, IP específico e firewall restrito. O aplicativo deliberadamente não executa inteiro como administrador; somente a alteração de firewall pede elevação.
