import typescript from '@rollup/plugin-typescript';
import resolve from '@rollup/plugin-node-resolve';

export default {
  input: 'src/index.ts',
  output: {
    file: 'com.systts.sdPlugin/bin/index.js',
    format: 'esm',
    sourcemap: true
  },
  external: ['path', 'fs', 'os', 'url'],
  plugins: [
    typescript({
      tsconfig: './tsconfig.json'
    }),
    resolve({
      preferBuiltins: true
    })
  ]
};
