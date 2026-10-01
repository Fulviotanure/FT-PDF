const { execSync } = require('child_process');
const readline = require('readline');
const path = require('path');

const rl = readline.createInterface({
  input: process.stdin,
  output: process.stdout
});

console.log('\n=============================================================');
console.log('   LANÇAMENTO OFICIAL FT PDF LITE (GITHUB ACTIONS)');
console.log('   Arquivo Único Standalone Self-Contained');
console.log('=============================================================');

rl.question('Digite o número da versão para lançamento (ex: 2.4.2): ', (verInput) => {
  let rawVer = verInput.trim().replace(/^v/i, '').replace(/^lite-v/i, '');

  if (!rawVer || !/^\d+\.\d+(\.\d+)?$/.test(rawVer)) {
    console.error(`\n❌ Versão inválida: "${verInput}". Use o formato X.Y ou X.Y.Z (ex: 2.4.2)`);
    rl.close();
    process.exit(1);
  }

  const tagName = `v${rawVer}`;
  const label = `FT PDF Lite v${rawVer} (Arquivo Único)`;

  console.log('\n-------------------------------------------------------------');
  console.log(`📦 Aplicativo: ${label}`);
  console.log(`🏷️ Tag Git que será criada: ${tagName}`);
  console.log('-------------------------------------------------------------');

  rl.question(`Confirma o lançamento da tag ${tagName} no GitHub? (s/n): `, (confirm) => {
    if (confirm.trim().toLowerCase() !== 's') {
      console.log('\nOperação cancelada pelo usuário.');
      rl.close();
      process.exit(0);
    }

    try {
      const rootDir = path.resolve(__dirname, '..');

      console.log('\n1. Preparando arquivos para commit...');
      execSync('git add .', { stdio: 'inherit', cwd: rootDir });

      console.log(`2. Criando commit de release (${tagName})...`);
      try {
        execSync(`git commit -m "chore(release): ${tagName}"`, { stdio: 'inherit', cwd: rootDir });
      } catch (e) {
        console.log('   Nenhuma alteração pendente para commit.');
      }

      console.log(`3. Criando tag Git: ${tagName}...`);
      execSync(`git tag -a ${tagName} -m "Release ${tagName}"`, { stdio: 'inherit', cwd: rootDir });

      console.log('4. Enviando commit e tag para o GitHub...');
      execSync('git push origin HEAD', { stdio: 'inherit', cwd: rootDir });
      execSync(`git push origin ${tagName}`, { stdio: 'inherit', cwd: rootDir });

      console.log('\n=============================================================');
      console.log(`🎉 SUCESSO! A tag ${tagName} foi enviada para o GitHub!`);
      console.log('O GitHub Actions já iniciou a compilação e publicação automática do executável único.');
      console.log('O download estará disponível em instantes em:');
      console.log('https://github.com/Fulviotanure/ft-pdf/releases');
      console.log('=============================================================\n');
    } catch (err) {
      console.error('\n❌ Erro durante o processo do Git:', err.message);
    } finally {
      rl.close();
    }
  });
});
