using System;
using System.Collections.Generic;
using UnityEngine;

public class RandomAnimation : MonoBehaviour
{
	[Serializable]
	public class RandomValue
	{
		public string m_name;

		public int m_values;

		public float m_interval;

		public bool m_floatValue;

		public float m_floatTransition = 1f;

		[NonSerialized]
		public float m_timer;

		[NonSerialized]
		public int m_value;

		[NonSerialized]
		public int[] m_hashValues;
	}

	public List<RandomValue> m_values = new List<RandomValue>();

	private Animator m_anim;

	private ZNetView m_nview;

	private void Start()
	{
		m_anim = GetComponentInChildren<Animator>();
		m_nview = GetComponent<ZNetView>();
	}

	private void FixedUpdate()
	{
		if (m_nview != null && !m_nview.IsValid())
		{
			return;
		}
		float fixedDeltaTime = Time.fixedDeltaTime;
		for (int i = 0; i < m_values.Count; i++)
		{
			RandomValue randomValue = m_values[i];
			if (m_nview == null || m_nview.IsOwner())
			{
				randomValue.m_timer += fixedDeltaTime;
				if (randomValue.m_timer > randomValue.m_interval)
				{
					randomValue.m_timer = 0f;
					randomValue.m_value = UnityEngine.Random.Range(0, randomValue.m_values);
					if ((bool)m_nview)
					{
						m_nview.GetZDO().Set("RA_" + randomValue.m_name, randomValue.m_value);
					}
					if (!randomValue.m_floatValue)
					{
						m_anim.SetInteger(randomValue.m_name, randomValue.m_value);
					}
				}
			}
			if ((bool)m_nview && !m_nview.IsOwner())
			{
				int @int = m_nview.GetZDO().GetInt("RA_" + randomValue.m_name);
				if (@int != randomValue.m_value)
				{
					randomValue.m_value = @int;
					if (!randomValue.m_floatValue)
					{
						m_anim.SetInteger(randomValue.m_name, randomValue.m_value);
					}
				}
			}
			if (!randomValue.m_floatValue)
			{
				continue;
			}
			if (randomValue.m_hashValues == null || randomValue.m_hashValues.Length != randomValue.m_values)
			{
				randomValue.m_hashValues = new int[randomValue.m_values];
				for (int j = 0; j < randomValue.m_values; j++)
				{
					randomValue.m_hashValues[j] = Animator.StringToHash(randomValue.m_name + j);
				}
			}
			for (int k = 0; k < randomValue.m_values; k++)
			{
				float @float = m_anim.GetFloat(randomValue.m_hashValues[k]);
				@float = ((k != randomValue.m_value) ? Mathf.MoveTowards(@float, 0f, fixedDeltaTime / randomValue.m_floatTransition) : Mathf.MoveTowards(@float, 1f, fixedDeltaTime / randomValue.m_floatTransition));
				m_anim.SetFloat(randomValue.m_hashValues[k], @float);
			}
		}
	}
}
