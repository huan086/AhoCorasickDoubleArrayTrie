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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Overby.Extensions.AsyncBinaryReaderWriter;

internal sealed class Write7BitEncodedBinaryWriter : AsyncBinaryWriter {
	public Write7BitEncodedBinaryWriter(Stream stream)
		: base(stream, new UTF8Encoding(false, true), true) { }

	public Task WriteIntAsync(int value, CancellationToken cancellationToken) =>
		base.Write7BitEncodedIntAsync(value, cancellationToken);

	public async Task WriteIntListAsync(IList<int> values, CancellationToken cancellationToken) {
		await base.Write7BitEncodedIntAsync(values.Count, cancellationToken).ConfigureAwait(false);
		foreach (int value in values) {
			await base.Write7BitEncodedIntAsync(value, cancellationToken).ConfigureAwait(false);
		}
	}

	public async Task WriteIntIntListAsync(IList<IList<int>?> values, CancellationToken cancellationToken) {
		await base.Write7BitEncodedIntAsync(values.Count, cancellationToken).ConfigureAwait(false);
		foreach (IList<int>? value in values) {
			if (value == null) {
				await base.Write7BitEncodedIntAsync(-1, cancellationToken).ConfigureAwait(false);
			} else {
				await this.WriteIntListAsync(value, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	internal static readonly Func<Write7BitEncodedBinaryWriter, object, CancellationToken, Task>[] TypeCodeWriters =
		new Func<Write7BitEncodedBinaryWriter, object, CancellationToken, Task>[]
		{
			(_, _, _) => Task.CompletedTask,
			(_, _, _) => throw new NotSupportedException(), //write object
			(_, _, _) => Task.CompletedTask,
			async (wr, o, ct) => await wr.WriteAsync((bool)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((char)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((sbyte)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((byte)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((short)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((ushort)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((int)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((uint)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((long)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((ulong)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((float)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((double)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync((decimal)o, ct).ConfigureAwait(false),
			async (wr, o, ct) => await wr.WriteAsync(((DateTime)o).ToBinary(), ct).ConfigureAwait(false),
			(_, _, _) => Task.CompletedTask, // 17 - not used typecode
			async (wr, o, ct) => await wr.WriteAsync(Convert.ToString(o, CultureInfo.InvariantCulture), ct).ConfigureAwait(false),
		};
}
