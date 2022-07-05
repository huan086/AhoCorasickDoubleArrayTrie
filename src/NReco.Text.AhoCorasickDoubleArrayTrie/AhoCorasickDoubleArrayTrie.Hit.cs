/*
 *  Copyright 2017 Vitalii Fedorchenko
 *
 *  This program is free software; you can redistribute it and/or modify
 *  it under the terms of the Apache License version 2.
 *
 *  This C# implementation is a port of hankcs's https://github.com/hankcs/AhoCorasickDoubleArrayTrie (java)
 *  that licensed under the Apache 2.0 License (see http://www.apache.org/licenses/LICENSE-2.0).
 *
 *  Unless required by applicable law or agreed to in writing, software distributed on an
 *  "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 */

namespace NReco.Text;

using System;
using System.Globalization;

/// <summary>
/// A match result.
/// </summary>
/// <typeparam name="TValue">The type of value returned when search strings match.</typeparam>
public struct AhoCorasickDoubleArrayTrieHit<TValue> : IEquatable<AhoCorasickDoubleArrayTrieHit<TValue>> {
	/// <summary>
	/// The beginning index, inclusive.
	/// </summary>
	public int Begin { get; }

	/// <summary>
	/// The ending index, exclusive.
	/// </summary>
	public int End { get; }

	/// <summary>
	/// The length of matched substring.
	/// </summary>
	public int Length => this.End - this.Begin;

	/// <summary>
	/// The value assigned to the keyword.
	/// </summary>
	public TValue? Value { get; }

	/// <summary>
	/// The index of the keyword
	/// </summary>
	public int Index { get; }

	public AhoCorasickDoubleArrayTrieHit(int begin, int end, TValue? value, int index) {
		this.Begin = begin;
		this.End = end;
		this.Value = value;
		this.Index = index;
	}

	public override string ToString() =>
		string.Format(CultureInfo.InvariantCulture, "[{0}:{1}]={2}", Begin, End, Value);

	public override bool Equals(object? obj) =>
		obj is AhoCorasickDoubleArrayTrieHit<TValue> other
			&& this.Equals(other);

	public override int GetHashCode() =>
		HashCode.Combine(this.Begin, this.End, this.Index, this.Value);

	public static bool operator ==(AhoCorasickDoubleArrayTrieHit<TValue> left, AhoCorasickDoubleArrayTrieHit<TValue> right) =>
		left.Equals(right);

	public static bool operator !=(AhoCorasickDoubleArrayTrieHit<TValue> left, AhoCorasickDoubleArrayTrieHit<TValue> right) =>
		!(left == right);

	public bool Equals(AhoCorasickDoubleArrayTrieHit<TValue> other) =>
		this.Begin.Equals(other.Begin)
			&& this.End.Equals(other.End)
			&& this.Index.Equals(other.Index)
			&& ((this.Value is null && other.Value is null) || (this.Value is not null && this.Value.Equals(other.Value)));
}
