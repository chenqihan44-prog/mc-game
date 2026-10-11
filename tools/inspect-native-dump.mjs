// Read-only Windows dump/PE diagnostics. Stack pointers are candidates, never
// an unwound call stack. Use inspectNativeDump({ dump, image, faultRva, output }).
import { readFile, writeFile } from 'node:fs/promises';
import { createHash } from 'node:crypto';
import { resolve, win32 } from 'node:path';

const hash = data => createHash('sha256').update(data).digest('hex');
const u32 = (data, offset) => data.readUInt32LE(offset);
const u64 = (data, offset) => Number(data.readBigUInt64LE(offset));
const hex = value => '0x' + value.toString(16);

async function inspectPe(path, faultRva) {
    const data = await readFile(path), nt = u32(data, 0x3c);
    if (data.toString('ascii', 0, 2) !== 'MZ' || data.toString('ascii', nt, nt + 4) !== 'PE\0\0') throw new Error('Not a PE image');
    const count = data.readUInt16LE(nt + 6), optionalSize = data.readUInt16LE(nt + 20), opt = nt + 24;
    if (data.readUInt16LE(opt) !== 0x20b) throw new Error('Only PE32+ images are supported');
    const sections = [];
    for (let i = 0; i < count; i++) {
        const at = opt + optionalSize + i * 40;
        sections.push({ name: data.toString('ascii', at, at + 8).replace(/\0.*$/, ''),
            virtualSize: u32(data, at + 8), rva: u32(data, at + 12), rawSize: u32(data, at + 16), raw: u32(data, at + 20) });
    }
    function raw(rva) {
        const section = sections.find(s => s.rva <= rva && rva < s.rva + s.rawSize);
        if (!section) throw new Error('RVA has no file data: ' + hex(rva));
        return section.raw + rva - section.rva;
    }
    const result = { path: resolve(path), sha256: hash(data), imageSize: u32(data, opt + 56),
        nativeSymbols: u32(data, nt + 16), sections, codeView: [], faultRva: hex(faultRva), functionRange: null };
    const faultOffset = raw(faultRva);
    result.faultBytes = [...data.subarray(faultOffset, faultOffset + 24)].map(b => b.toString(16).padStart(2, '0')).join(' ');
    const debugRva = u32(data, opt + 112 + 6 * 8), debugSize = u32(data, opt + 112 + 6 * 8 + 4);
    if (debugRva && debugSize) {
        const start = raw(debugRva);
        for (let at = start; at < start + debugSize; at += 28) {
            const kind = u32(data, at + 12), size = u32(data, at + 16), ptr = u32(data, at + 24);
            if (kind === 2 && data.toString('ascii', ptr, ptr + 4) === 'RSDS') result.codeView.push({
                guidBytes: data.subarray(ptr + 4, ptr + 20).toString('hex'), age: u32(data, ptr + 20),
                pdbPath: data.toString('utf8', ptr + 24, ptr + size).replace(/\0.*$/, '') });
        }
    }
    const exceptionRva = u32(data, opt + 112 + 3 * 8), exceptionSize = u32(data, opt + 112 + 3 * 8 + 4);
    if (exceptionRva) {
        const start = raw(exceptionRva);
        for (let at = start; at < start + exceptionSize; at += 12) {
            const begin = u32(data, at), end = u32(data, at + 4), unwind = u32(data, at + 8);
            if (begin <= faultRva && faultRva < end) {
                result.functionRange = { begin: hex(begin), end: hex(end), unwindRva: hex(unwind) };
                break;
            }
        }
    }
    return result;
}

export async function inspectNativeDump({ dump, image, faultRva, output }) {
    const data = await readFile(dump);
    if (data.toString('ascii', 0, 4) !== 'MDMP') throw new Error('Not a Windows minidump');
    const count = u32(data, 8), directory = u32(data, 12), streams = new Map();
    for (let i = 0; i < count; i++) {
        const at = directory + i * 12;
        streams.set(u32(data, at), { size: u32(data, at + 4), rva: u32(data, at + 8) });
    }
    if (!streams.has(7) || data.readUInt16LE(streams.get(7).rva) !== 9) throw new Error('Only AMD64 minidumps are supported');
    const modules = [], threads = [], stackRegions = [];
    if (streams.has(4)) {
        const at = streams.get(4).rva;
        for (let i = 0; i < u32(data, at); i++) {
            const entry = at + 4 + i * 108, nameAt = u32(data, entry + 20), nameLen = u32(data, nameAt);
            modules.push({ path: data.toString('utf16le', nameAt + 4, nameAt + 4 + nameLen), base: u64(data, entry), size: u32(data, entry + 8) });
        }
    }
    function locate(address) {
        const module = modules.find(m => m.base <= address && address < m.base + m.size);
        return module ? { module: win32.basename(module.path), rva: hex(address - module.base) } : null;
    }
    if (streams.has(3)) {
        const at = streams.get(3).rva;
        for (let i = 0; i < u32(data, at); i++) {
            const entry = at + 4 + i * 48, stackStart = u64(data, entry + 24), stackSize = u32(data, entry + 32),
                stackAt = u32(data, entry + 36), contextSize = u32(data, entry + 40), contextAt = u32(data, entry + 44);
            const thread = { id: u32(data, entry), contextBytes: contextSize, stackStart: hex(stackStart), stackBytes: stackSize };
            stackRegions.push({ threadId: thread.id, fileStart: stackAt, bytes: stackSize, virtualStart: stackStart });
            if (contextSize >= 256) {
                const rip = u64(data, contextAt + 248), rsp = u64(data, contextAt + 152);
                Object.assign(thread, { rip: hex(rip), rsp: hex(rsp), instruction: locate(rip), registers: {}, sampledStackModulePointersNotCallStack: [] });
                for (const [name, offset] of Object.entries({ rax: 120, rcx: 128, rdx: 136, rbx: 144, rbp: 160, rsi: 168, rdi: 176 })) thread.registers[name] = hex(u64(data, contextAt + offset));
                if (stackStart <= rsp && rsp < stackStart + stackSize) {
                    const start = stackAt + rsp - stackStart, end = Math.min(start + 256, stackAt + stackSize);
                    for (let offset = start; offset + 8 <= end; offset += 8) {
                        const address = u64(data, offset), match = locate(address);
                        if (match) thread.sampledStackModulePointersNotCallStack.push({ stackOffset: offset - start, address: hex(address), ...match });
                    }
                }
            }
            threads.push(thread);
        }
    }
    let exception = null;
    if (streams.has(6)) {
        const at = streams.get(6).rva;
        exception = { threadId: u32(data, at), code: hex(u32(data, at + 8)), address: hex(u64(data, at + 24)) };
    }
    // A normal dump may omit ExceptionStream yet retain RtlDispatchException's
    // saved structures on the faulting stack. Match both layouts independently;
    // mark them as recovered candidates rather than pretending the stream exists.
    const imageModule = modules.find(m => win32.basename(m.path) === win32.basename(image));
    const recoveredStackCandidates = { exceptionRecords: [], contexts: [] };
    if (imageModule) {
        const address = imageModule.base + faultRva, pattern = Buffer.alloc(8);
        pattern.writeBigUInt64LE(BigInt(address));
        for (const region of stackRegions) {
            const end = region.fileStart + region.bytes;
            for (let at = data.indexOf(pattern, region.fileStart); at >= 0 && at + 8 <= end; at = data.indexOf(pattern, at + 1)) {
                const record = at - 16;
                if (record >= region.fileStart && record + 152 <= end && u32(data, record) === 0xc0000005 && u32(data, record + 24) === 2) {
                    recoveredStackCandidates.exceptionRecords.push({ threadId: region.threadId, dumpOffset: record,
                        code: '0xc0000005', flags: u32(data, record + 4), address: hex(address),
                        accessKind: u64(data, record + 32), accessedAddress: hex(u64(data, record + 40)) });
                }
                const context = at - 248;
                if (context >= region.fileStart && context + 1232 <= end) {
                    const flags = u32(data, context + 48), rsp = u64(data, context + 152);
                    if ((flags & 0x100003) === 0x100003 && region.virtualStart <= rsp && rsp < region.virtualStart + region.bytes) {
                        recoveredStackCandidates.contexts.push({ threadId: region.threadId, dumpOffset: context, flags: hex(flags),
                            rip: hex(address), rsp: hex(rsp), rcx: hex(u64(data, context + 128)), rdx: hex(u64(data, context + 136)) });
                    }
                }
            }
        }
    }
    const result = { dump: resolve(dump), bytes: data.length, sha256: hash(data), streamTypes: [...streams.keys()].sort((a, b) => a - b),
        originalExceptionContextPresent: !!exception, exception, recoveredStackCandidates, modules, threads,
        limitation: 'Captured threads are not necessarily the original fault context; sampled stack pointers are not an unwound call stack.',
        image: await inspectPe(image, faultRva) };
    await writeFile(output, JSON.stringify(result, null, 2), 'utf8');
    return { threads: threads.length, originalExceptionContextPresent: !!exception, nativeSymbols: result.image.nativeSymbols,
        recoveredStackCandidates, codeView: result.image.codeView, faultBytes: result.image.faultBytes, functionRange: result.image.functionRange };
}
