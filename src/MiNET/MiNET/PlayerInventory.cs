﻿using System.Collections.Generic;
using System.Linq;
using log4net;
using MiNET.Items;
using MiNET.Net;
using MiNET.Utils;

namespace MiNET
{
	public class PlayerInventory
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (PlayerInventory));

		public const int HotbarSize = 9;
		public const int InventorySize = HotbarSize + 36;
		public Player Player { get; private set; }

		public List<Item> Slots { get; private set; }
		public int[] ItemHotbar { get; private set; }
		public int InHandSlot { get; set; }


		// Armour
		public Item Boots { get; set; }
		public Item Leggings { get; set; }
		public Item Chest { get; set; }
		public Item Helmet { get; set; }

		public PlayerInventory(Player player)
		{
			Player = player;

			Slots = Enumerable.Repeat((Item) new ItemAir(), InventorySize).ToList();

			ItemHotbar = new int[HotbarSize];
			for (byte i = 0; i < ItemHotbar.Length; i++)
			{
				ItemHotbar[i] = i;
			}

			InHandSlot = 0;

			Boots = new ItemAir();
			Leggings = new ItemAir();
			Chest = new ItemAir();
			Helmet = new ItemAir();
		}

		public virtual Item GetItemInHand()
		{
			var index = ItemHotbar[InHandSlot];
			if (index == -1 || index >= Slots.Count) return new ItemAir();

			return Slots[index] ?? new ItemAir();
		}

		[Wired]
		public void SetInventorySlot(int slot, Item item)
		{
			Slots[slot] = item;

			SendSetSlot(slot);
		}

		public MetadataInts GetHotbar()
		{
			MetadataInts metadata = new MetadataInts();
			for (byte i = 0; i < ItemHotbar.Length; i++)
			{
				if (ItemHotbar[i] == -1)
				{
					metadata[i] = new MetadataInt(-1);
				}
				else
				{
					metadata[i] = new MetadataInt(ItemHotbar[i] + HotbarSize);
				}
			}

			return metadata;
		}

		public ItemStacks GetSlots()
		{
			ItemStacks slotData = new ItemStacks();
			for (int i = 0; i < Slots.Count; i++)
			{
				if (Slots[i].Count == 0) Slots[i] = new ItemAir();
				slotData.Add(Slots[i]);
			}

			return slotData;
		}

		public ItemStacks GetArmor()
		{
			return new ItemStacks
			{
				Helmet ?? new ItemAir(),
				Chest ?? new ItemAir(),
				Leggings ?? new ItemAir(),
				Boots ?? new ItemAir(),
			};
		}

		public bool SetFirstEmptySlot(Item item, bool update, bool reverseOrder)
		{
			if (reverseOrder)
			{
				for (int si = Slots.Count; si > 0; si--)
				{
					if (FirstEmptySlot(item, update, si - 1)) return true;
				}
			}
			else
			{
				for (int si = 0; si < Slots.Count; si++)
				{
					if (FirstEmptySlot(item, update, si)) return true;
				}
			}

			return false;
		}

		private bool FirstEmptySlot(Item item, bool update, int si)
		{
			Item existingItem = Slots[si];

			if (existingItem.Id == item.Id && existingItem.Metadata == item.Metadata && existingItem.Count + item.Count <= item.MaxStackSize)
			{
				Slots[si].Count += item.Count;
				//if (update) Player.SendPlayerInventory();
				if (update) SendSetSlot(si);
				return true;
			}
			else if (existingItem is ItemAir || existingItem.Id == -1)
			{
				Slots[si] = item;
				//if (update) Player.SendPlayerInventory();
				if (update) SendSetSlot(si);
				return true;
			}

			return false;
		}

		public void SetHeldItemSlot(int selectedHotbarSlot, bool sendToPlayer = true)
		{
			InHandSlot = selectedHotbarSlot;

			if (sendToPlayer)
			{
				McpePlayerEquipment order = McpePlayerEquipment.CreateObject();
				order.entityId = 0;
				order.item = GetItemInHand();
				order.selectedSlot = (byte) selectedHotbarSlot;
				order.slot = (byte) ItemHotbar[InHandSlot];
				Player.SendPackage(order);
			}

			McpePlayerEquipment broadcast = McpePlayerEquipment.CreateObject();
			broadcast.entityId = Player.EntityId;
			broadcast.item = GetItemInHand();
			broadcast.selectedSlot = (byte) selectedHotbarSlot;
			broadcast.slot = (byte) ItemHotbar[InHandSlot];
			Player.Level?.RelayBroadcast(broadcast);
		}

		/// <summary>
		///     Empty the specified slot
		/// </summary>
		/// <param name="slot">The slot to empty.</param>
		public void ClearInventorySlot(byte slot)
		{
			SetInventorySlot(slot, new ItemAir());
		}

		public bool HasItem(Item item)
		{
			for (byte i = 0; i < Slots.Count; i++)
			{
				if ((Slots[i]).Id == item.Id && (Slots[i]).Metadata == item.Metadata)
				{
					return true;
				}
			}
			return false;
		}

		public void RemoveItems(short id, byte count)
		{
			for (byte i = 0; i < Slots.Count; i++)
			{
				var slot = Slots[i];
				if (slot.Id == id)
				{
					slot.Count--;
					if (slot.Count == 0)
					{
						Slots[i] = new ItemAir();
					}

					SendSetSlot(i);
					return;
				}
			}
		}

		public void SendSetSlot(int slot)
		{
			if (slot < HotbarSize && (ItemHotbar[slot] == -1 || ItemHotbar[slot] == slot))
			{
				ItemHotbar[slot] = slot /* + HotbarSize*/;
				Player.SendPlayerInventory();

				McpePlayerEquipment order = McpePlayerEquipment.CreateObject();
				order.entityId = 0;
				order.item = GetItemInHand();
				order.selectedSlot = (byte) slot; // Selected hotbar slot
				Player.SendPackage(order);
			}
			else
			{
				McpeContainerSetSlot sendSlot = McpeContainerSetSlot.CreateObject();
				sendSlot.windowId = 0;
				sendSlot.slot = (short) slot;
				sendSlot.item = Slots[slot];
				Player.SendPackage(sendSlot);
			}
		}

		public void Clear()
		{
			for (int i = 0; i < Slots.Count; ++i)
			{
				if (Slots[i] == null || Slots[i].Id != 0) Slots[i] = new ItemAir();
			}

			if (Helmet.Id != 0) Helmet = new ItemAir();
			if (Chest.Id != 0) Chest = new ItemAir();
			if (Leggings.Id != 0) Leggings = new ItemAir();
			if (Boots.Id != 0) Boots = new ItemAir();

			Player.SendPlayerInventory();
		}
	}
}