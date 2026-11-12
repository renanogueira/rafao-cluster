# Tutorial — Rafão Cluster

Este roteiro configura primeiro um piloto com dois computadores Windows e depois amplia para os demais.

## Antes de começar

- Todos os computadores precisam estar na mesma rede cabeada dedicada.
- Use endereços fixos, por exemplo: coordenador `10.77.0.10`, workers `10.77.0.11`, `10.77.0.12` e assim por diante.
- Não coloque gateway nessa interface dedicada e não encaminhe a porta `50052` no roteador.
- Atualize o driver NVIDIA em todos os computadores.
- Copie `RafaoCluster.exe` para cada computador.

## 1. Preparar o primeiro worker

1. Abra `RafaoCluster.exe` no computador que emprestará a GPU.
2. Selecione **Worker**.
3. Clique em **Diagnosticar** e confira GPU, VRAM, driver, IP e velocidade da rede.
4. Clique em **Instalar / atualizar**. O aplicativo baixa a mesma versão CUDA do `llama.cpp`.
5. Em **IP dedicado deste worker**, informe o IP dessa máquina, por exemplo `10.77.0.11`.
6. Em **IP autorizado do coordenador**, informe `10.77.0.10`.
7. Mantenha porta RPC `50052`, dispositivo `CUDA0` e cache ativado.
8. Clique em **Configurar firewall** e aceite somente depois de conferir os dois IPs exibidos.
9. Clique em **Iniciar worker** e deixe o aplicativo aberto.

O log deve mostrar o servidor RPC escutando no IP dedicado e a GPU CUDA detectada.

## 2. Preparar o coordenador

1. Abra o aplicativo no PC coordenador.
2. Selecione **Coordenador**.
3. Clique em **Diagnosticar**.
4. Clique em **Instalar / atualizar**.
5. Em **Workers**, informe `10.77.0.11:50052`. Para vários workers, use vírgulas: `10.77.0.11:50052,10.77.0.12:50052`.

Para executar somente neste computador, deixe **Workers** vazio. O coordenador usará a GPU local; quando houver endereços na lista, ele combinará a GPU local com os workers RPC.

## 3. Baixar o primeiro GGUF automaticamente

1. Ao lado de **Modelo GGUF**, clique em **Baixar GGUF**.
2. Escolha um item no **Catálogo público e gratuito**. Comece pelo Gemma 3 1B para validar a rede; depois avance para Qwen3 14B ou modelos maiores para testar a distribuição.
3. O repositório será preenchido automaticamente. Clique em **Consultar arquivos**.
4. Escolha o arquivo `Q4_K_M`.
5. Mantenha a pasta sugerida `C:\llama\models`.
6. Deixe o token vazio para modelos públicos. Para modelos restritos, aceite a licença no Hugging Face e informe um token de leitura; o aplicativo não salva esse token.
7. Clique em **Baixar e selecionar**.

O download é feito diretamente do Hugging Face e o campo do modelo é preenchido quando termina. A documentação oficial recomenda downloads de arquivos específicos do Hub e permite uma pasta local; o aplicativo implementa esse fluxo para arquivos `.gguf`.

O catálogo contém somente repositórios públicos e baixáveis sem token. A licença aparece ao lado de cada modelo. O campo de repositório continua editável para modelos fora da lista.

## 4. Executar o teste de dois nós

1. Mantenha contexto `4096` e porta da API `8080`.
2. Clique em **Iniciar coordenador**.
3. O programa testa primeiro se cada worker responde pela rede.
4. Aguarde o log indicar que o modelo foi carregado e o servidor está pronto.
5. Abra `http://127.0.0.1:8080` no navegador do coordenador.
6. Envie uma mensagem curta e acompanhe o painel **Execução** do worker. Ele mostra a GPU, VRAM utilizada, utilização instantânea e a contagem de grafos executados, sem precisar abrir `nvidia-smi` ou o Gerenciador de Tarefas.

O indicador **Calculando** e o aumento da contagem de grafos são a confirmação de que a GPU remota participou da inferência. A utilização pode voltar momentaneamente a 0% entre tokens sem indicar falha.

## 5. Passar para um modelo maior

O modelo de 1B serve apenas para validar rede, instalação e API. Depois:

1. Some a VRAM das GPUs e desconte aproximadamente 1–2 GB de cada uma.
2. No downloader, informe um repositório GGUF maior.
3. Comece com `Q4_K_M`, que geralmente oferece bom equilíbrio entre qualidade e memória.
4. Escolha um arquivo que caiba na VRAM útil combinada, deixando espaço para o contexto/KV cache.
5. Teste primeiro com contexto `4096` e somente depois aumente.

Orientação inicial:

| VRAM útil combinada | Primeiro alvo razoável |
|---:|---|
| 8–12 GB | 7B/8B em Q4_K_M |
| 16–24 GB | 14B–32B quantizado, conforme arquitetura |
| 32–48 GB | 32B ou alguns 70B em quantização menor |
| 48 GB+ | 70B Q4 ou MoE maiores, validando arquivo e KV cache |

O tamanho do arquivo não é o único consumo: contexto, KV cache e buffers também usam VRAM.

## 6. Adicionar os outros workers

1. Repita a preparação de Worker em uma máquina por vez.
2. Use IP exclusivo em cada uma.
3. Pare o coordenador.
4. Acrescente o novo `IP:50052` à lista.
5. Inicie novamente e confirme que o teste TCP passa para todos.

## Solução rápida de problemas

- **Worker não respondeu:** confirme IP, porta, perfil de rede Privada, firewall e se o worker está iniciado.
- **CUDA0 não apareceu:** atualize o driver NVIDIA e execute novamente a instalação do `llama.cpp`.
- **Sem memória:** use GGUF menor, quantização menor ou contexto menor.
- **Download bloqueado:** aceite a licença do modelo no Hugging Face e use um token de leitura.
- **Geração muito lenta:** teste cabo/switch, evite Wi‑Fi e compare 1 GbE com 2.5/10 GbE.
- **Windows exibiu Editor desconhecido:** o executável ainda não tem assinatura de código; confirme que o hash corresponde ao arquivo entregue.

## Segurança

O backend RPC do `llama.cpp` é experimental. Use somente rede dedicada ou VLAN confiável, restrinja o firewall ao coordenador e nunca exponha a porta RPC à internet.
