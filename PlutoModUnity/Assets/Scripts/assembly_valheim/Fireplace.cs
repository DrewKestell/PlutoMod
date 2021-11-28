using System;
using UnityEngine;

public class Fireplace : MonoBehaviour, Hoverable, Interactable
{
	private ZNetView m_nview;

	private Piece m_piece;

	[Header("Fire")]
	public string m_name = "Fire";

	public float m_startFuel = 3f;

	public float m_maxFuel = 10f;

	public float m_secPerFuel = 3f;

	public float m_checkTerrainOffset = 0.2f;

	public float m_coverCheckOffset = 0.5f;

	private const float m_minimumOpenSpace = 0.5f;

	public float m_holdRepeatInterval = 0.2f;

	public GameObject m_enabledObject;

	public GameObject m_enabledObjectLow;

	public GameObject m_enabledObjectHigh;

	public GameObject m_playerBaseObject;

	public ItemDrop m_fuelItem;

	public SmokeSpawner m_smokeSpawner;

	public EffectList m_fuelAddedEffects = new EffectList();

	[Header("Fireworks")]
	public ItemDrop m_fireworkItem;

	public int m_fireworkItems = 2;

	public GameObject m_fireworks;

	private bool m_blocked;

	private bool m_wet;

	private float m_lastUseTime;

	private static int m_solidRayMask;

	public void Awake()
	{
		m_nview = base.gameObject.GetComponent<ZNetView>();
		m_piece = base.gameObject.GetComponent<Piece>();
		if (m_nview.GetZDO() == null)
		{
			return;
		}
		if (m_solidRayMask == 0)
		{
			m_solidRayMask = LayerMask.GetMask("Default", "static_solid", "Default_small", "piece", "terrain");
		}
		if (m_nview.IsOwner() && m_nview.GetZDO().GetFloat("fuel", -1f) == -1f)
		{
			m_nview.GetZDO().Set("fuel", m_startFuel);
			if (m_startFuel > 0f)
			{
				m_fuelAddedEffects.Create(base.transform.position, base.transform.rotation);
			}
		}
		m_nview.Register("AddFuel", RPC_AddFuel);
		InvokeRepeating("UpdateFireplace", 0f, 2f);
		InvokeRepeating("CheckEnv", 4f, 4f);
	}

	private void Start()
	{
		if ((bool)m_playerBaseObject && (bool)m_piece)
		{
			m_playerBaseObject.SetActive(m_piece.IsPlacedByPlayer());
		}
	}

	private double GetTimeSinceLastUpdate()
	{
		DateTime time = ZNet.instance.GetTime();
		DateTime dateTime = new DateTime(m_nview.GetZDO().GetLong("lastTime", time.Ticks));
		TimeSpan timeSpan = time - dateTime;
		m_nview.GetZDO().Set("lastTime", time.Ticks);
		double num = timeSpan.TotalSeconds;
		if (num < 0.0)
		{
			num = 0.0;
		}
		return num;
	}

	private void UpdateFireplace()
	{
		if (!m_nview.IsValid())
		{
			return;
		}
		if (m_nview.IsOwner())
		{
			float @float = m_nview.GetZDO().GetFloat("fuel");
			double timeSinceLastUpdate = GetTimeSinceLastUpdate();
			if (IsBurning())
			{
				float num = (float)(timeSinceLastUpdate / (double)m_secPerFuel);
				@float -= num;
				if (@float <= 0f)
				{
					@float = 0f;
				}
				m_nview.GetZDO().Set("fuel", @float);
			}
		}
		UpdateState();
	}

	private void CheckEnv()
	{
		CheckUnderTerrain();
		if (m_enabledObjectLow != null && m_enabledObjectHigh != null)
		{
			CheckWet();
		}
	}

	private void CheckUnderTerrain()
	{
		m_blocked = false;
		RaycastHit hitInfo;
		if (Heightmap.GetHeight(base.transform.position, out var height) && height > base.transform.position.y + m_checkTerrainOffset)
		{
			m_blocked = true;
		}
		else if (Physics.Raycast(base.transform.position + Vector3.up * m_coverCheckOffset, Vector3.up, out hitInfo, 0.5f, m_solidRayMask))
		{
			m_blocked = true;
		}
		else if ((bool)m_smokeSpawner && m_smokeSpawner.IsBlocked())
		{
			m_blocked = true;
		}
	}

	private void CheckWet()
	{
		Cover.GetCoverForPoint(base.transform.position + Vector3.up * m_coverCheckOffset, out var coverPercentage, out var underRoof);
		m_wet = false;
		if (EnvMan.instance.GetWindIntensity() >= 0.8f && coverPercentage < 0.7f)
		{
			m_wet = true;
		}
		if (EnvMan.instance.IsWet() && !underRoof)
		{
			m_wet = true;
		}
	}

	private void UpdateState()
	{
		if (IsBurning())
		{
			m_enabledObject.SetActive(value: true);
			if ((bool)m_enabledObjectHigh && (bool)m_enabledObjectLow)
			{
				m_enabledObjectHigh.SetActive(!m_wet);
				m_enabledObjectLow.SetActive(m_wet);
			}
		}
		else
		{
			m_enabledObject.SetActive(value: false);
			if ((bool)m_enabledObjectHigh && (bool)m_enabledObjectLow)
			{
				m_enabledObjectLow.SetActive(value: false);
				m_enabledObjectHigh.SetActive(value: false);
			}
		}
	}

	public string GetHoverText()
	{
		float @float = m_nview.GetZDO().GetFloat("fuel");
		return Localization.instance.Localize(m_name + " ( $piece_fire_fuel " + Mathf.Ceil(@float) + "/" + (int)m_maxFuel + " )\n[<color=yellow><b>$KEY_Use</b></color>] $piece_use " + m_fuelItem.m_itemData.m_shared.m_name + "\n[<color=yellow><b>1-8</b></color>] $piece_useitem");
	}

	public string GetHoverName()
	{
		return m_name;
	}

	public bool Interact(Humanoid user, bool hold, bool alt)
	{
		if (hold)
		{
			if (m_holdRepeatInterval <= 0f)
			{
				return false;
			}
			if (Time.time - m_lastUseTime < m_holdRepeatInterval)
			{
				return false;
			}
		}
		if (!m_nview.HasOwner())
		{
			m_nview.ClaimOwnership();
		}
		Inventory inventory = user.GetInventory();
		if (inventory != null)
		{
			if (inventory.HaveItem(m_fuelItem.m_itemData.m_shared.m_name))
			{
				if ((float)Mathf.CeilToInt(m_nview.GetZDO().GetFloat("fuel")) >= m_maxFuel)
				{
					user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_cantaddmore", m_fuelItem.m_itemData.m_shared.m_name));
					return false;
				}
				user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_fireadding", m_fuelItem.m_itemData.m_shared.m_name));
				inventory.RemoveItem(m_fuelItem.m_itemData.m_shared.m_name, 1);
				m_nview.InvokeRPC("AddFuel");
				return true;
			}
			user.Message(MessageHud.MessageType.Center, "$msg_outof " + m_fuelItem.m_itemData.m_shared.m_name);
			return false;
		}
		return true;
	}

	public bool UseItem(Humanoid user, ItemDrop.ItemData item)
	{
		if (item.m_shared.m_name == m_fuelItem.m_itemData.m_shared.m_name)
		{
			if ((float)Mathf.CeilToInt(m_nview.GetZDO().GetFloat("fuel")) >= m_maxFuel)
			{
				user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_cantaddmore", item.m_shared.m_name));
				return true;
			}
			Inventory inventory = user.GetInventory();
			user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_fireadding", item.m_shared.m_name));
			inventory.RemoveItem(item, 1);
			m_nview.InvokeRPC("AddFuel");
			return true;
		}
		if (m_fireworkItem != null && item.m_shared.m_name == m_fireworkItem.m_itemData.m_shared.m_name)
		{
			if (!IsBurning())
			{
				user.Message(MessageHud.MessageType.Center, "$msg_firenotburning");
				return true;
			}
			if (user.GetInventory().CountItems(m_fireworkItem.m_itemData.m_shared.m_name) < m_fireworkItems)
			{
				user.Message(MessageHud.MessageType.Center, "$msg_toofew " + m_fireworkItem.m_itemData.m_shared.m_name);
				return true;
			}
			user.GetInventory().RemoveItem(item.m_shared.m_name, m_fireworkItems);
			user.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$msg_throwinfire", item.m_shared.m_name));
			ZNetScene.instance.SpawnObject(base.transform.position, Quaternion.identity, m_fireworks);
			m_fuelAddedEffects.Create(base.transform.position, base.transform.rotation);
			return true;
		}
		return false;
	}

	private void RPC_AddFuel(long sender)
	{
		if (m_nview.IsOwner())
		{
			float @float = m_nview.GetZDO().GetFloat("fuel");
			if (!((float)Mathf.CeilToInt(@float) >= m_maxFuel))
			{
				@float = Mathf.Clamp(@float, 0f, m_maxFuel);
				@float += 1f;
				@float = Mathf.Clamp(@float, 0f, m_maxFuel);
				m_nview.GetZDO().Set("fuel", @float);
				m_fuelAddedEffects.Create(base.transform.position, base.transform.rotation);
				UpdateState();
			}
		}
	}

	public bool CanBeRemoved()
	{
		return !IsBurning();
	}

	public bool IsBurning()
	{
		if (m_blocked)
		{
			return false;
		}
		float liquidLevel = Floating.GetLiquidLevel(m_enabledObject.transform.position);
		if (m_enabledObject.transform.position.y < liquidLevel)
		{
			return false;
		}
		return m_nview.GetZDO().GetFloat("fuel") > 0f;
	}

	private void OnDrawGizmosSelected()
	{
		Gizmos.color = Color.white;
		Gizmos.DrawWireSphere(base.transform.position + Vector3.up * m_coverCheckOffset, 0.5f);
		Gizmos.color = Color.yellow;
		Gizmos.DrawWireCube(base.transform.position + Vector3.up * m_checkTerrainOffset, new Vector3(1f, 0.01f, 1f));
	}
}
