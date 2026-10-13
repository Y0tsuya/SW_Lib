using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

/*	Original copier description:
	Routines to convert 12 bit linear samples to the
	Dialogic or Oki ADPCM coding format.
	I copied the algorithms out of the book "PC Telephony - The
	complete guide to designing, building and programming systems
	using Dialogic and Related Hardware" by Bob Edgar. pg 272-276.
	Note: Edgar's book says that the second to last value is 1408; however,
	* The standard says it is 1411.
	* Changed on 1/17/2003.
*/

public class AudLib {
	public enum Direction { Undef, Encode, Decode };

	public class AdpcmState {
		public int Last;
		public int StepIndex;
		public AdpcmState() {
			Last = 0;
			StepIndex = 0;
		}
	}

	public class WavHeader {
		public byte[] riffID; // "riff"
		public uint size;  // ファイルサイズ-8
		public byte[] wavID;  // "WAVE"
		public byte[] fmtID;  // "fmt "
		public uint fmtSize; // fmtチャンクのバイト数
		public ushort format; // フォーマット
		public ushort channels; // チャンネル数
		public uint sampleRate; // サンプリングレート
		public uint bytePerSec; // データ速度
		public ushort blockSize; // ブロックサイズ
		public ushort bit;  // 量子化ビット数
		public byte[] dataID; // "data"
		public uint dataSize; // 波形データのバイト数

		public WavHeader() {
		}
		public WavHeader(int streamSize, uint rate) {
			riffID = new byte[] { 0x52, 0x49, 0x46, 0x46 };
			size = (uint)(streamSize * 2 + 36);
			wavID = new byte[] { 0x57, 0x41, 0x56, 0x45 };
			fmtID = new byte[] { 0x66, 0x6D, 0x74, 0x20 };
			fmtSize = 16;
			format = 1;
			channels = 1;
			sampleRate = rate;
			bytePerSec = (uint)(rate * 2);  // 16-bit mono
			blockSize = 2;  // 16-bit mono
			bit = 16;
			dataID = new byte[] { 0x64, 0x61, 0x74, 0x61 };
			dataSize = (uint)(streamSize * 2);
		}

	}

	public class AUD1Header {
		public char[] Tag;
		public byte AID, Type, Resol;
		public int Sample;
		public int Size;
		public int Length;

		public AUD1Header() {
			Tag = new char[] { 'A', 'U', 'D', '1' };
			AID = 0x00;
			Type = 0x00;
			Resol = 8;
			Sample = 8000;
			Size = 0;
			Length = 16;
		}

		public byte[] Serialize() {
			int i;
			byte[] rbyte = new byte[Length];
			for (i = 0; i < 4; i++) {
				rbyte[i] = (byte)Tag[i];
			}
			rbyte[4] = (byte)AID;
			rbyte[5] = (byte)Type;
			rbyte[6] = 0x00;
			rbyte[7] = (byte)Resol;
			rbyte[8] = (byte)(Sample);
			rbyte[9] = (byte)(Sample >> 8);
			rbyte[10] = (byte)(Sample >> 16);
			rbyte[11] = (byte)(Sample >> 24);
			for (i = 0; i < 4; i++) {
				rbyte[i + 12] = (byte)(Size >> (i * 8));
			}

			return rbyte;
		}

		public void Deserialize(byte[] blob) {
			int i;
			Size = 0;
			Sample = 0;
			for (i = 0; i < 4; i++) {
				Tag[i] = (char)blob[i];
			}
			AID = blob[4];
			Type = blob[5];
			Resol = blob[7];
			for (i = 0; i < 4; i++) {
				Sample = Sample | blob[i + 8] << (i * 8);
			}
			for (i = 0; i < 4; i++) {
				Size = Size | blob[i + 12] << (i * 8);
			}
		}
	}

	public static short[] StepSize = new short[] {
		16, 17, 19, 21, 23, 25, 28, 31,
		34, 37, 41, 45, 50, 55, 60, 66,
		73, 80, 88, 97, 107, 118, 130, 143,
		157, 173, 190, 209, 230, 253, 279, 307,
		337, 371, 408, 449, 494, 544, 598, 658,
		724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552 };

	public static short[] StepAdjust = new short[] { -1, -1, -1, -1, 2, 4, 6, 8 };

	public static short[] ReadWave(string fileName, out WavHeader header) {
		short[] PcmStream;
		FileStream fs;
		byte sam;

		header = new WavHeader();
		try {
			fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
		} catch (IOException ex) {
			Console.WriteLine("Error opening " + fileName);
			Console.WriteLine(ex.Message);
			return null;
		}
		BinaryReader br = new BinaryReader(fs);
		try {
			header.riffID = br.ReadBytes(4);
			header.size = br.ReadUInt32();
			header.wavID = br.ReadBytes(4);
			header.fmtID = br.ReadBytes(4);
			header.fmtSize = br.ReadUInt32();
			header.format = br.ReadUInt16();
			header.channels = br.ReadUInt16();
			header.sampleRate = br.ReadUInt32();
			header.bytePerSec = br.ReadUInt32();
			header.blockSize = br.ReadUInt16();
			header.bit = br.ReadUInt16();
			header.dataID = br.ReadBytes(4);
			header.dataSize = br.ReadUInt32();

			if ((header.blockSize / (header.bit / 8)) == 2) {
				Console.WriteLine("adpcm only supports mono input");
				return null;
			}

			PcmStream = new short[header.dataSize / header.blockSize];

			for (int i = 0; i < header.dataSize / header.blockSize; i++) {
				if (header.bit == 16) {
					PcmStream[i] = (short)br.ReadUInt16();
				} else {
					sam = br.ReadByte();
					PcmStream[i] = (short)((sam - 0x80) << 8);
				}
				PcmStream[i] = (short)(PcmStream[i] >> 4);	// 16-bit to 12-bit scaling
			}
		} catch (IOException ex) {
			Console.WriteLine("Error processing " + fileName);
			Console.WriteLine(ex.Message);
			return null;
		}
		fs.Close();
		br.Close();
		return PcmStream;
	}

	public static bool WriteWave(string fileName, short[] pcmStream, uint rate) {
		WavHeader Header = new WavHeader(pcmStream.Count(), rate);
		FileStream fs;

		try {
			fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
		} catch (IOException ex) {
			Console.WriteLine("Error opening " + fileName);
			Console.WriteLine(ex.Message);
			return false;
		}
		BinaryWriter bw = new BinaryWriter(fs);
		try {
			bw.Write(Header.riffID);
			bw.Write(Header.size);
			bw.Write(Header.wavID);
			bw.Write(Header.fmtID);
			bw.Write(Header.fmtSize);
			bw.Write(Header.format);
			bw.Write(Header.channels);
			bw.Write(Header.sampleRate);
			bw.Write(Header.bytePerSec);
			bw.Write(Header.blockSize);
			bw.Write(Header.bit);
			bw.Write(Header.dataID);
			bw.Write(Header.dataSize);

			for (int i = 0; i < Header.dataSize / Header.blockSize; i++) {
				pcmStream[i] = (short)(pcmStream[i] << 4);	// 12-bit to 16-bit scaling
				if (i < pcmStream.Count()) {
					bw.Write((ushort)pcmStream[i]);
				} else {
					bw.Write(0);
				}
			}
		} catch (IOException ex) {
			Console.WriteLine("Error processing " + fileName);
			Console.WriteLine(ex.Message);
			return false;
		}
		bw.Close();
		fs.Close();
		return true;
	}

	public static byte[] ReadAdpcm(string fileName, out uint sampleRate) {
		byte[] adpcmStream;
		FileStream fs;
		AUD1Header header = new AUD1Header();
		byte[] headerBlob;

		sampleRate = 0;
		try {
			fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
		} catch (IOException ex) {
			Console.WriteLine("Error opening " + fileName);
			Console.WriteLine(ex.Message);
			return null;
		}
		BinaryReader br = new BinaryReader(fs);
		try {
			headerBlob = br.ReadBytes(header.Length);
			header.Deserialize(headerBlob);
			adpcmStream = br.ReadBytes(header.Size);
			sampleRate = (uint)header.Sample;
		} catch (Exception ex) {
			Console.WriteLine("Error processing " + fileName);
			Console.WriteLine(ex.Message);
			return null;
		}
		return adpcmStream;
	}

	public static bool WriteAdpcm(string fileName, byte[] adpcmStream, uint sampleRate) {
		FileStream fs;
		AUD1Header header = new AUD1Header();
		byte[] headerBlob;

		try {
			fs = new FileStream(fileName, FileMode.Create, FileAccess.Write);
		} catch (IOException ex) {
			Console.WriteLine("Error opening " + fileName);
			Console.WriteLine(ex.Message);
			return false;
		}
		BinaryWriter bw = new BinaryWriter(fs);
		header.Sample = (int)sampleRate;
		header.Resol = 4;
		header.Type = 0x03;
		header.Size = adpcmStream.Count();
		try {
			headerBlob = header.Serialize();
			bw.Write(headerBlob);
			bw.Write(adpcmStream);
		} catch (Exception ex) {
			Console.WriteLine("Error processing " + fileName);
			Console.WriteLine(ex.Message);
			return false;
		}
		return true;
	}

	public static byte AdpcmEncode(short sample, AdpcmState state) {
		int code;
		int diff, E, SS;

		SS = StepSize[state.StepIndex];
		code = 0x00;
		if ((diff = sample - state.Last) < 0)
			code = 0x08;
		E = diff < 0 ? -diff : diff;
		if (E >= SS) {
			code = code | 0x04;
			E -= SS;
		}
		if (E >= SS / 2) {
			code = code | 0x02;
			E -= SS / 2;
		}
		if (E >= SS / 4) {
			code = code | 0x01;
		}
		/*	stat->step_index += step_adjust( code );
			if( stat->step_index < 0 ) stat->step_index = 0;
			if( stat->step_index > 48 ) stat->step_index = 48;
		* Use the decoder to set the estimate of last sample.
		* It also will adjust the step_index for us.
		*/
		state.Last = AdpcmDecode((byte)code, state);
		return (byte)code;
	}
	public static short AdpcmDecode(byte code, AdpcmState state) {
		int diff, E, SS, sample;

		/* printf( "%x\t", code );
		*/
		SS = StepSize[state.StepIndex];
		E = SS / 8;
		if ((code & 0x01) == 0x01)
			E += SS / 4;
		if ((code & 0x02) == 0x02)
			E += SS / 2;
		if ((code & 0x04) == 0x04)
			E += SS;
		diff = ((code & 0x08) == 0x08) ? -E : E;
		sample = state.Last + diff;

		/*
		*  Clip the values to +(2^11)-1 to -2^11. (12 bits 2's
		*  compelement)
		*  Note: previous version errantly clipped at +2048, which could
		*  cause a 2's complement overflow and was likely the source of
		*  clipping problems in the previous version.  Thanks to Frank
		*  van Dijk for the correction.  TLB 3/30/04
		*/
		if (sample > 2047) {
			sample = 2047;
		}
		if (sample < -2048) {
			sample = -2048;
		}

		state.Last = sample;
		state.StepIndex += StepAdjust[code & 0x07];
		if (state.StepIndex < 0) state.StepIndex = 0;
		if (state.StepIndex > 48) state.StepIndex = 48;

		/* printf( "%d\n", samp );
		*/
		return (short)sample;
	}

	public static short[] AdpcmDecode(byte[] adpcm) {
		int sampleCount = adpcm.Count() * 2;
		int i;
		short[] pcmStream = new short[sampleCount];
		byte a1, a2;
		AdpcmState state = new AdpcmState();

		for (i = 0; i < adpcm.Count(); i++) {
			a1 = (byte)(adpcm[i] >> 4);
			a2 = (byte)(adpcm[i] & 0x0F);
			pcmStream[i * 2] = AdpcmDecode(a1, state);
			pcmStream[i * 2 + 1] = AdpcmDecode(a2, state);
		}
		return pcmStream;
	}

	public static byte[] AdpcmEncode(short[] pcm) {
		int sampleCount = pcm.Count();
		int i;
		byte[] adpcmStream = new byte[sampleCount/2];
		short p1, p2;
		byte a1, a2;
		AdpcmState state = new AdpcmState();

		for (i = 0; i < sampleCount/2; i++) {
			p1 = pcm[i * 2];
			p2 = pcm[i * 2 + 1];
			a1 = AdpcmEncode(p1, state);
			a2 = AdpcmEncode(p2, state);
			adpcmStream[i] = (byte)((a1<<4)|a2);
		}
		return adpcmStream;
	}

	public class NormSignalInfo {
		public double RMS;
		public short MaxSample;
		public short MinSample;

		public NormSignalInfo() {
			RMS = 0;
			MaxSample = Int16.MinValue;
			MinSample = Int16.MaxValue;
		}
	}

	public static NormSignalInfo ComputeRmsLevel(short[] pcmStream) {
		int len = pcmStream.Length;
		double sum = 0;
		NormSignalInfo info = new NormSignalInfo();
		short sample;

		for (int i = 0; i < len; i++) {
			sample = pcmStream[i];
			sum += sample * sample;
			if (sample > info.MaxSample) info.MaxSample = sample;
			if (sample < info.MinSample) info.MinSample = sample;
		}
		double pow = sum / len;
		info.RMS = Math.Sqrt(pow);
		return info;
	}


}
