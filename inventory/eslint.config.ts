import eslint from '@eslint/js';
import { defineConfig } from 'eslint/config';
import tseslint from 'typescript-eslint';

export default defineConfig(
  eslint.configs.recommended,
  tseslint.configs.recommended,
  {
    rules: {
      'semi': ['error', 'always'],
      'no-extra-semi': 'error',
      'indent': ['error', 2],
      'quotes': ['error', 'single'],
    },
  },
  {
    files: ['**/*.ts']
  }
);
