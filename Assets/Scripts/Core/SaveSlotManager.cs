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

        /// <summary>
        /// Saves game data to the specified slot. Overwrites any existing data.
        /// Sets slot state to HasSave.
        /// </summary>
        /// <param name="slotId">Zero-based slot identifier.</param>
        /// <param name="data">Save data to store.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when slotId is out of range.</exception>
        /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
        public void Save(int slotId, SaveData data)
        {
            ValidateSlotId(slotId);
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            var slot = _slots[slotId];
            slot.Data = data;
            slot.State = SlotState.HasSave;
        }

        /// <summary>
        /// Loads save data from the specified slot.
        /// </summary>
        /// <param name="slotId">Zero-based slot identifier.</param>
        /// <returns>The saved SaveData, or null if the slot is empty.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when slotId is out of range.</exception>
        public SaveData Load(int slotId)
        {
            ValidateSlotId(slotId);
            return _slots[slotId].Data;
        }

        /// <summary>
        /// Checks whether the specified slot contains valid save data.
        /// </summary>
        public bool HasSave(int slotId)
        {
            ValidateSlotId(slotId);
            return _slots[slotId].State == SlotState.HasSave;
        }

        /// <summary>
        /// Clears the specified slot, returning it to Empty state.
        /// </summary>
        public void Clear(int slotId)
        {
            ValidateSlotId(slotId);
            var slot = _slots[slotId];
            slot.Data = null;
            slot.State = SlotState.Empty;
        }

        /// <summary>
        /// Returns the raw SaveSlot for the given id, or null if out of range.
        /// </summary>
        public SaveSlot GetSlot(int slotId)
        {
            if (slotId < 0 || slotId >= _maxSlots)
                return null;
            return _slots[slotId];
        }

        private void ValidateSlotId(int slotId)
        {
            if (slotId < 0 || slotId >= _maxSlots)
                throw new ArgumentOutOfRangeException(nameof(slotId), $"Slot id {slotId} is out of range [0, {_maxSlots - 1}].");
        }
    }

    /// <summary>
    /// Per-slot data holder for SaveSlotManager.
    /// </summary>
    public class SaveSlot
    {
        public int SlotId { get; internal set; }
        public SaveSlotManager.SlotState State { get; internal set; }
        public SaveData Data { get; internal set; }
    }
}
