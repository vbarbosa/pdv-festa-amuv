using Xunit;

// Testes E2E dirigem a UI REAL (mouse/teclado/foco). NUNCA podem rodar em paralelo:
// duas janelas do app disputando o foco travam a suite. Desabilita o paralelismo do
// assembly inteiro — cinto de seguranca alem da collection "e2e".
[assembly: CollectionBehavior(DisableTestParallelization = true, MaxParallelThreads = 1)]
