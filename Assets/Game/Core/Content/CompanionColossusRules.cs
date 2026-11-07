using System;
using System.Collections.Generic;

namespace Game.Core.Content
{
    public sealed class ColossusForm
    {
        public ColossusForm(string id, string answer, string nameRu, string nameEn)
        {
            Id = id;
            Answer = answer;
            NameRu = nameRu;
            NameEn = nameEn;
        }

        public string Id { get; }
        public string Answer { get; }
        public string NameRu { get; }
        public string NameEn { get; }
    }

    public sealed class CompanionColossusRules
    {
        public int Hours { get; private set; } = 12;
        public int Letters { get; private set; } = 3;
        public IReadOnlyList<string> Answers { get; private set; } = Array.Empty<string>();
        public string DefaultAnswer { get; private set; } = string.Empty;
        public IReadOnlyList<ColossusForm> Forms { get; private set; } = Array.Empty<ColossusForm>();
        public double Numbers { get; private set; }

        public bool Known(string answer)
        {
            for (var i = 0; i < Answers.Count; i++)
            {
                if (Answers[i] == answer)
                    return true;
            }
            return false;
        }

        public ColossusForm? FormFor(string answer)
        {
            for (var i = 0; i < Forms.Count; i++)
            {
                if (Forms[i].Answer == answer)
                    return Forms[i];
            }
            return null;
        }

        public ColossusForm? Form(string id)
        {
            for (var i = 0; i < Forms.Count; i++)
            {
                if (Forms[i].Id == id)
                    return Forms[i];
            }
            return null;
        }

        public ColossusForm? Choose(IReadOnlyList<string> answers)
        {
            var best = string.Empty;
            var most = 0;
            for (var i = answers.Count - 1; i >= 0; i--)
            {
                var votes = 0;
                for (var j = 0; j < answers.Count; j++)
                {
                    if (answers[j] == answers[i])
                        votes++;
                }
                if (votes > most)
                {
                    most = votes;
                    best = answers[i];
                }
            }
            return FormFor(best.Length > 0 ? best : DefaultAnswer);
        }

        internal static CompanionColossusRules Load(ContentNode node)
        {
            var answers = new List<string>();
            foreach (var answer in node.Field("answers").Items())
                answers.Add(answer.AsString());
            var forms = new List<ColossusForm>();
            foreach (var item in node.Field("forms").Items())
            {
                var answer = item.Field("answer").AsString();
                if (!answers.Contains(answer))
                    throw item.Field("answer").Error($"unknown colossus answer '{answer}'");
                var name = item.Field("name");
                forms.Add(new ColossusForm(item.Field("id").AsString(), answer, name.Field("ru").AsString(), name.Field("en").AsString()));
            }
            var fallback = node.Field("defaultAnswer").AsString();
            if (!answers.Contains(fallback))
                throw node.Field("defaultAnswer").Error($"unknown colossus answer '{fallback}'");
            return new CompanionColossusRules
            {
                Hours = node.Field("hours").AsInt(1, 48),
                Letters = node.Field("letters").AsInt(0, 10),
                Answers = answers,
                DefaultAnswer = fallback,
                Forms = forms,
                Numbers = node.Field("numbers").AsNumber(0, 1)
            };
        }
    }
}
