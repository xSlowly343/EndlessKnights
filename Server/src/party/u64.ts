type U64 = number[];

function u64FromDecimal(text: string): U64 {
    let value: U64 = [0, 0, 0, 0];
    for (let i = 0; i < text.length; i++) {
        const digit = text.charCodeAt(i) - 48;
        if (digit < 0 || digit > 9)
            throw new Error("not a decimal u64: " + text);
        value = u64Add(u64Mul(value, u64FromNumber(10)), u64FromNumber(digit));
    }
    return value;
}

function u64FromNumber(value: number): U64 {
    const limbs: U64 = [0, 0, 0, 0];
    let rest = Math.floor(value);
    for (let i = 0; i < 4; i++) {
        limbs[i] = rest % 65536;
        rest = Math.floor(rest / 65536);
    }
    return limbs;
}

function u64ToDecimal(value: U64): string {
    const limbs = value.slice();
    let digits = "";
    do {
        let remainder = 0;
        for (let i = 3; i >= 0; i--) {
            const current = remainder * 65536 + limbs[i];
            limbs[i] = Math.floor(current / 10);
            remainder = current % 10;
        }
        digits = String(remainder) + digits;
    } while (limbs[0] !== 0 || limbs[1] !== 0 || limbs[2] !== 0 || limbs[3] !== 0);
    return digits;
}

function u64Add(a: U64, b: U64): U64 {
    const sum: U64 = [0, 0, 0, 0];
    let carry = 0;
    for (let i = 0; i < 4; i++) {
        const total = a[i] + b[i] + carry;
        sum[i] = total % 65536;
        carry = total >= 65536 ? 1 : 0;
    }
    return sum;
}

function u64Mul(a: U64, b: U64): U64 {
    const product: U64 = [0, 0, 0, 0];
    for (let i = 0; i < 4; i++) {
        let carry = 0;
        for (let j = 0; i + j < 4; j++) {
            const total = product[i + j] + a[i] * b[j] + carry;
            product[i + j] = total % 65536;
            carry = Math.floor(total / 65536);
        }
    }
    return product;
}

function u64Xor(a: U64, b: U64): U64 {
    return [a[0] ^ b[0], a[1] ^ b[1], a[2] ^ b[2], a[3] ^ b[3]];
}

function u64ShiftRight(a: U64, bits: number): U64 {
    const result: U64 = [0, 0, 0, 0];
    const limbShift = Math.floor(bits / 16);
    const bitShift = bits % 16;
    for (let i = 0; i < 4; i++) {
        const low = i + limbShift < 4 ? a[i + limbShift] : 0;
        const high = i + limbShift + 1 < 4 ? a[i + limbShift + 1] : 0;
        result[i] = bitShift === 0 ? low : ((low >>> bitShift) | (high << (16 - bitShift))) & 0xffff;
    }
    return result;
}

const SplitMixGamma = u64FromDecimal("11400714819323198485");
const SplitMixFirst = u64FromDecimal("13787848793156543929");
const SplitMixSecond = u64FromDecimal("10723151780598845931");

function partySeedFor(seed: string, enemyId: number): string {
    let z = u64Add(u64FromDecimal(seed), u64Mul(SplitMixGamma, u64FromNumber(enemyId)));
    z = u64Mul(u64Xor(z, u64ShiftRight(z, 30)), SplitMixFirst);
    z = u64Mul(u64Xor(z, u64ShiftRight(z, 27)), SplitMixSecond);
    return u64ToDecimal(u64Xor(z, u64ShiftRight(z, 31)));
}
