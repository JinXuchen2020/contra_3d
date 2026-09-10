using System;
using System.Collections.Generic;

namespace Contra3D.Core
{
    /// <summary>
    /// T-BDD-ADOPT (rg_save_slots_initialized): SaveSlotManager initialized at startup.
    /// Manages save slot data with a configurable maximum number of slots.
    /// All slots start in Empty state until data is written.
    /// </summary>
    public class SaveSlotManager
    {
        public enum SlotState { Empty, HasSave }

        private readonly List<SaveSlot> _slots;
        private readonly int _maxSlots;

        /// <summary>Maximum number of save slots available.</summary>
        public int MaxSlots => _maxSlots;

        /// <summary>Current slot states indexed by slot id.</summary>
        public IReadOnlyList<SaveSlot> Slots => _slots;

        public SaveSlotManager(int maxSlots = 3)
        {
            if (maxSlots < 1)
                throw new ArgumentException("max_slots must be >= 1", nameof(maxSlots));
            _maxSlots = maxSlots;
            _slots = new List<SaveSlot>(maxSlots);
            for (int i = 0; i < maxSlots; i++)
                _slots.Add(new SaveSlot { SlotId = i, State = SlotState.Empty, Data = null });
        }
    }

    /// <summary>
    /// Per-slot data holder for SaveSlotManager.
    /// </summary>
    public class SaveSlot
    {
        public int SlotId { get; set; }
        public SaveSlotManager.SlotState State { get; set; }
        public SaveData Data { get; set; }
    }
}
