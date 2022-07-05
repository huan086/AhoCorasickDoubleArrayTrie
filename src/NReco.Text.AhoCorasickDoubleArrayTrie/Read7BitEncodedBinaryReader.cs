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
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Overby.Extensions.AsyncBinaryReaderWriter;

internal class Read7BitEncodedBinaryReader : AsyncBinaryReader {
	public Read7BitEncodedBinaryReader(Stream stream)
		: base(stream, new UTF8Encoding(), true) {
	}

	public Task<int> ReadIntAsync(CancellationToken cancellationToken) =>
		base.Read7BitEncodedIntAsync(cancellationToken);

	public async Task<int[]> ReadIntListAsync(CancellationToken cancellationToken) {
		int length = await base.Read7BitEncodedIntAsync(cancellationToken).ConfigureAwait(false);
		var values = new int[length];
		for (int i = 0; i < values.Length; i++) {
			values[i] = await base.Read7BitEncodedIntAsync(cancellationToken).ConfigureAwait(false);
		}

		return values;
	}

	public async Task<int[]?[]> ReadIntIntListAsync(CancellationToken cancellationToken) {
		int length = await base.Read7BitEncodedIntAsync(cancellationToken).ConfigureAwait(false);
		int[]?[] values = new int[length][];
		for (int i = 0; i < values.Length; i++) {
			values[i] = await this.ReadIntNullableListAsync(cancellationToken).ConfigureAwait(false);
		}

		return values;
	}

	internal static readonly Func<Read7BitEncodedBinaryReader, CancellationToken, Task<object?>>[] TypeCodeReaders =
		new Func<Read7BitEncodedBinaryReader, CancellationToken, Task<object?>>[]
		{
			(_, _) => Task.FromResult<object?>(null), // null
			(_, _) => throw new NotSupportedException(), // read object!!
			(_, _) => Task.FromResult<object?>(DBNull.Value), // dbnull
			async (rdr, ct) => await rdr.ReadBooleanAsync(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadCharAsync(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadSByteAsync(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadByteAsync(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadInt16Async(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadUInt16Async(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadInt32Async(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadUInt32Async(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadInt64Async(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadUInt64Async(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadSingleAsync(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadDoubleAsync(ct).ConfigureAwait(false),
			async (rdr, ct) => await rdr.ReadDecimalAsync(ct).ConfigureAwait(false),
			async (rdr, ct) => DateTime.FromBinary(await rdr.ReadInt64Async(ct).ConfigureAwait(false)),
			(_, _) => Task.FromResult<object?>(null), // 17 - not used typecode
			async (rdr, ct) => await rdr.ReadStringAsync(ct).ConfigureAwait(false),
		};

	private async Task<int[]?> ReadIntNullableListAsync(CancellationToken cancellationToken) {
		int length = await base.Read7BitEncodedIntAsync(cancellationToken).ConfigureAwait(false);
		if (length == -1) {
			return null;
		}

		var values = new int[length];
		for (int i = 0; i < values.Length; i++) {
			values[i] = await base.Read7BitEncodedIntAsync(cancellationToken).ConfigureAwait(false);
		}

		return values;
	}
}
