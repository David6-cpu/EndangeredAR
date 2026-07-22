using System;

namespace EndangeredAR.UI
{
    internal readonly struct ChatRequestTicket
    {
        public ChatRequestTicket(string animalId, int generation)
        {
            AnimalId = animalId;
            Generation = generation;
        }

        public string AnimalId { get; }
        public int Generation { get; }
    }

    internal sealed class ChatRequestState
    {
        private int generation;
        private ChatRequestTicket activeTicket;

        public bool IsThinking { get; private set; }

        public ChatRequestTicket Begin(string animalId)
        {
            generation++;
            activeTicket = new ChatRequestTicket(animalId?.Trim() ?? string.Empty, generation);
            IsThinking = true;
            return activeTicket;
        }

        public bool InvalidateForAnimalChange(string animalId)
        {
            if (!IsThinking || string.Equals(activeTicket.AnimalId, animalId?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            Invalidate();
            return true;
        }

        public void Invalidate()
        {
            generation++;
            activeTicket = default;
            IsThinking = false;
        }

        public bool CanComplete(ChatRequestTicket ticket, string currentAnimalId)
        {
            return IsThinking &&
                   ticket.Generation == activeTicket.Generation &&
                   string.Equals(ticket.AnimalId, activeTicket.AnimalId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(ticket.AnimalId, currentAnimalId?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        public bool TryComplete(ChatRequestTicket ticket, string currentAnimalId)
        {
            if (!CanComplete(ticket, currentAnimalId))
            {
                return false;
            }

            Invalidate();
            return true;
        }
    }
}
