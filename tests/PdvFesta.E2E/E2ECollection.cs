using Xunit;

namespace PdvFesta.E2E;

/// <summary>
/// Define a collection "e2e" para SERIALIZAR os testes de interface. Sem esta definicao,
/// o atributo [Collection("e2e")] nos testes seria IGNORADO e o xUnit rodaria todos em
/// paralelo — varias janelas do app abertas ao mesmo tempo brigando por foco/teclado,
/// travando a suite. Testes na mesma collection nunca rodam em paralelo entre si.
/// </summary>
[CollectionDefinition("e2e", DisableParallelization = true)]
public sealed class E2ECollection { }
