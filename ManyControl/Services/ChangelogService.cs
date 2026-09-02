namespace ManyControl.Services;

public class DestaqueInfo
{
    public string Tipo { get; set; } = "Novidade"; // Novidade, Melhoria, Ajuste
    public string Titulo { get; set; } = string.Empty;
    public string Descricao { get; set; } = string.Empty;
    public string Icone { get; set; } = "★";
    public string CorBadge => Tipo switch
    {
        "Novidade" => "#065F46", // Verde escuro
        "Melhoria" => "#4338CA", // Roxo / Índigo
        _ => "#0369A1"           // Azul / Ajuste
    };
    public string CorTextoBadge => Tipo switch
    {
        "Novidade" => "#A7F3D0",
        "Melhoria" => "#C7D2FE",
        _ => "#BAE6FD"
    };
}

public class VersaoInfo
{
    public string Numero { get; set; } = string.Empty;
    public string DataLancamento { get; set; } = string.Empty;
    public string Titulo { get; set; } = string.Empty;
    public bool IsAtual { get; set; }
    public List<DestaqueInfo> Destaques { get; set; } = [];
}

public class ChangelogService
{
    private readonly List<VersaoInfo> _historico =
    [
        new VersaoInfo
        {
            Numero = "v1.0.17",
            DataLancamento = "02/09/2026",
            Titulo = "Tema Claro e Visual Aprimorado",
            IsAtual = true,
            Destaques =
            [
                new DestaqueInfo
                {
                    Tipo = "Novidade",
                    Titulo = "Tema Claro (Modo Dia)",
                    Descricao = "Novo visual claro com alto contraste e leitura suave, ideal para usar durante o dia ou em locais iluminados.",
                    Icone = "☀️"
                },
                new DestaqueInfo
                {
                    Tipo = "Melhoria",
                    Titulo = "Alternador Rápido de Tema",
                    Descricao = "Troque entre o modo claro e escuro a qualquer momento pelo botão no topo da tela ou na aba de Sincronização.",
                    Icone = "🌙"
                },
                new DestaqueInfo
                {
                    Tipo = "Novidade",
                    Titulo = "Controle de Receitas Recebidas",
                    Descricao = "Agora você pode marcar receitas como 'Já Recebida' ou 'A Receber', mantendo seu Saldo Real 100% preciso.",
                    Icone = "💰"
                }
            ]
        },
        new VersaoInfo
        {
            Numero = "v1.0.14",
            DataLancamento = "01/09/2026",
            Titulo = "Gestão de Receitas e Saldo Real",
            IsAtual = false,
            Destaques =
            [
                new DestaqueInfo
                {
                    Tipo = "Novidade",
                    Titulo = "Receitas Previstas vs. Efetivadas",
                    Descricao = "Valores futuros a receber agora não inflam o saldo atual da sua conta antes da data do recebimento.",
                    Icone = "✓"
                },
                new DestaqueInfo
                {
                    Tipo = "Melhoria",
                    Titulo = "Alternador Rápido de Status",
                    Descricao = "Marque uma receita como recebida com apenas um clique diretamente na tela inicial.",
                    Icone = "⚡"
                }
            ]
        },
        new VersaoInfo
        {
            Numero = "v1.0.13",
            DataLancamento = "31/08/2026",
            Titulo = "Atualizações Automáticas e Estabilidade",
            IsAtual = false,
            Destaques =
            [
                new DestaqueInfo
                {
                    Tipo = "Melhoria",
                    Titulo = "Atualização In-App Inteligente",
                    Descricao = "Tela de progresso com barra de download e reinício automático do app ao concluir a instalação.",
                    Icone = "🔄"
                },
                new DestaqueInfo
                {
                    Tipo = "Ajuste",
                    Titulo = "Seleção de Conta do Google",
                    Descricao = "Facilidade para escolher qual conta Google você deseja vincular para sincronização na nuvem.",
                    Icone = "🔒"
                }
            ]
        },
        new VersaoInfo
        {
            Numero = "v1.0.7",
            DataLancamento = "29/08/2026",
            Titulo = "Filtro de Meses e Despesas Pagas",
            IsAtual = false,
            Destaques =
            [
                new DestaqueInfo
                {
                    Tipo = "Novidade",
                    Titulo = "Navegação por Mês",
                    Descricao = "Veja seu extrato e histórico financeiro de qualquer mês passado ou futuro com facilidade.",
                    Icone = "📅"
                },
                new DestaqueInfo
                {
                    Tipo = "Novidade",
                    Titulo = "Status de Despesas (Paga vs Pendente)",
                    Descricao = "Acompanhe contas que já foram pagas e as que ainda vão vencer no mês.",
                    Icone = "⏳"
                }
            ]
        }
    ];

    public VersaoInfo GetVersaoAtual() => _historico.FirstOrDefault(v => v.IsAtual) ?? _historico[0];

    public List<VersaoInfo> GetHistorico() => _historico;
}
