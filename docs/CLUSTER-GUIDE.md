# Configurando o primeiro cluster

Comece com apenas dois computadores. Depois que o piloto funcionar, adicione os
outros workers um por vez.

## 1. Planejar a rede

Exemplo para a rede `192.168.0.0/24`:

| Papel | Endereço | Porta |
|---|---|---:|
| Coordenador | `192.168.0.10` | API `8080` local |
| Worker 1 | `192.168.0.11` | RPC `50052` |
| Worker 2 | `192.168.0.12` | RPC `50052` |

Use reservas DHCP ou IPs fixos. Confirme que as máquinas se alcançam antes de
abrir o aplicativo:

```text
ping 192.168.0.11
```

## 2. Preparar o worker

1. Abra o Rafão Cluster e escolha **Worker**.
2. Clique em **Diagnosticar**.
3. Instale ou atualize o `llama.cpp`.
4. Informe o IP local do worker.
5. Informe `50052` como porta RPC.
6. Selecione `CUDA0` no Windows/Linux NVIDIA ou `Metal0` no macOS.
7. Restrinja o firewall ao IP do coordenador.
8. Clique em **Iniciar worker**.

Repita **Instalar / atualizar** depois de qualquer atualização do Rafão Cluster.
Coordenador e workers precisam usar a mesma build do `llama.cpp`; a versão
0.4.4 usa `b12110` em todas as plataformas.

Mantenha o aplicativo aberto e confira no log se o servidor RPC está
escutando no IP correto.

## 3. Preparar o coordenador

1. Escolha **Coordenador**.
2. Instale ou atualize o `llama.cpp`.
3. Em **Workers**, clique em **Encontrar** para localizar automaticamente os
   workers ativos. Se preferir, informe manualmente `192.168.0.11:50052`.
4. Clique em **Baixar GGUF**.
5. Para o primeiro teste, use um modelo pequeno e uma quantização `Q4_K_M`.
6. Mantenha contexto `4096` e API `8080`.
7. Clique em **Iniciar coordenador**.

O programa testa a conexão TCP com cada worker antes de carregar o modelo.

## 4. Validar a API

No coordenador:

```bash
curl http://127.0.0.1:8080/health
curl http://127.0.0.1:8080/v1/models
```

Uma chamada compatível com a API OpenAI:

```bash
curl http://127.0.0.1:8080/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "model": "local",
    "messages": [{"role": "user", "content": "Olá!"}]
  }'
```

No PowerShell, use `curl.exe` para evitar o alias de `Invoke-WebRequest`.

## 5. Escolher um modelo maior

Some a memória disponível das GPUs e mantenha uma margem para contexto, KV
cache e buffers.

| Memória útil combinada | Alvo inicial aproximado |
|---:|---|
| 8–12 GB | 7B/8B em Q4 |
| 16–24 GB | 14B e alguns 32B quantizados |
| 32–48 GB | 32B ou 70B com quantização agressiva |
| 48 GB+ | 70B Q4 ou modelos MoE, após validação |

Esses valores são apenas pontos de partida: arquiteturas e tamanhos de contexto
mudam o consumo real.

## 6. Escalar

1. Pare o coordenador.
2. Prepare o próximo worker com um IP exclusivo.
3. Acrescente `IP:50052` à lista, separado por vírgula.
4. Reinicie o coordenador.
5. Confira o log e o uso de GPU em todas as máquinas.

