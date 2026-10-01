# Regra 1: Diretrizes de Edição, Pastas de Compilação e Fluxo de Teste/Atualização

Esta regra define o protocolo obrigatório para desenvolvimento, testes, compilação e deploy neste projeto:

1. **Edição Exclusivamente Local**:
   - Qualquer edição, melhoria ou refatoração deve ser realizada estritamente nos arquivos locais do projeto.
   - NUNCA disparar compilações nem enviar nada ao Git automaticamente durante as rodadas de edição.

2. **Área de Trabalho (Apenas Arquivo `.bat` de Teste Bruto)**:
   - Na Área de Trabalho deve permanecer **exclusivamente o arquivo `.bat` de teste local** (`Testar FT PDF Lite.bat`).
   - Esse script executa diretamente os arquivos brutos via `dotnet run`, permitindo testes imediatos das alterações sem gerar compilações prévias.
   - NUNCA salvar executáveis `.exe` pesados nem criar pastas na Área de Trabalho (para evitar que ferramentas como o Google Drive gerem pastas temporárias como `.tmp.driveupload`).

3. **Estrutura da Pasta `compilacoes/` (Versionamento por Pastas)**:
   - Toda e qualquer compilação solicitada deve ser salva exclusivamente dentro da pasta interna do projeto: `compilacoes/`.
   - **Cada nova compilação deve gerar uma nova subpasta identificando a versão** (ex: `compilacoes/v2.0.0/`, `compilacoes/v2.1.0/`), contendo seus respectivos `.exe`.
   - **Zero dependência de .NET:** Compilar sempre no modo Self-Contained (`--self-contained true`) para que o executável funcione direto em qualquer máquina sem solicitar instalação de runtimes do .NET.

4. **Fluxo Estrito de Lançamento e Atualização**:
   - **Passo 1 (Desenvolvimento):** Ajustar código localmente.
   - **Passo 2 (Teste em Desenvolvimento):** O usuário testa as alterações na hora usando o `.bat` da Área de Trabalho (arquivos brutos).
   - **Passo 3 (Solicitação de Compilação):** Quando o código estiver do jeito certo, o usuário pede expressamente a compilação indicando a nova versão. O agente compila e gera a nova pasta dentro de `compilacoes/vX.Y.Z/`.
   - **Passo 4 (Validação do Executável):** O usuário testa os executáveis finais na pasta de compilação.
   - **Passo 5 (Liberação para o GitHub):** Somente após a validação e autorização expressa do usuário (*"libero para atualização"*), criar a nova tag/versão e sincronizar com o GitHub para o acionamento do release automático.
