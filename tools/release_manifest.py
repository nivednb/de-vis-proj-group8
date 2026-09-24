"""Freeze an already validated Windows folder against the final Git source.
Run only after committing all source/docs. Does not build or imply validation.
"""
import argparse, hashlib, json, pathlib, shutil, subprocess, zipfile
from datetime import datetime, timezone

def git(root, *args):
    return subprocess.check_output(['git','-C',str(root),*args],text=True).strip()

def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--package',required=True)
    parser.add_argument('--build-source-commit',required=True)
    parser.add_argument('--build-log',required=True)
    args=parser.parse_args()
    root=pathlib.Path(__file__).resolve().parents[1]
    package=pathlib.Path(args.package).resolve()
    if git(root,'status','--porcelain'):
        raise SystemExit('Refusing to freeze a dirty source checkout.')
    commit=git(root,'rev-parse','HEAD')
    build_commit=git(root,'rev-parse',args.build_source_commit)
    if git(root,'diff',build_commit,commit,'--','Assets','Packages','ProjectSettings'):
        raise SystemExit('Build inputs differ from the recorded build source commit.')
    build_log=pathlib.Path(args.build_log).read_text(encoding='utf-8',errors='replace')
    import re
    match=re.search(r'PtMeOH Windows build result: Succeeded; errors: (\d+); warnings: (\d+)',build_log)
    if not match or match[1]!='0':raise SystemExit('Successful build evidence missing.')
    numerical=(root/'docs/evidence/numerical-validation.txt').read_text(encoding='utf-8')
    if 'PASS assertions=' not in numerical:raise SystemExit('Numerical validation evidence missing.')
    for name in ('final-runtime-1080','final-runtime-720'):
        report=(root/'docs/evidence'/name/'runtime-validation.txt').read_text(encoding='utf-8')
        if 'RUNTIME_VALIDATION_PASS' not in report or '\nFAIL ' in report:raise SystemExit('Runtime validation failed/missing: '+name)
        if name.endswith('1080'):
            seconds=float(re.search(r'SOAK_SECONDS ([\d.]+)',report)[1])
            if seconds<900:raise SystemExit('15-minute soak not completed.')
    if not (package/'PtMeOH-DigitalTwin.exe').is_file():raise SystemExit('Executable missing.')
    execution=json.loads((root/'docs/evidence/verification-execution.json').read_text(encoding='utf-8-sig'))
    if execution['source_commit']!=build_commit or not execution['all_stages_passed']:
        raise SystemExit('Build execution record does not match source.')
    if execution['library_existed_at_start'] or execution['initial_git_status']:
        raise SystemExit('Final validation did not start in a clean checkout without Library.')
    for name, expected in execution['tested_build_sha256'].items():
        if digest(package/name)!=expected:raise SystemExit('Tested binary changed: '+name)
    shutil.copytree(root/'docs' ,package/'docs',dirs_exist_ok=True)
    shutil.copy2(root/'README.md',package/'README.md')
    source_zip=package/'PtMeOH-Source.zip'
    subprocess.run(['git','-C',str(root),'archive','--format=zip','--prefix=PtMeOH-Source/','-o',str(source_zip),commit],check=True)
    files={p.relative_to(package).as_posix():digest(p) for p in sorted(package.rglob('*')) if p.is_file() and p!=package/'release-manifest.json'}
    manifest={
        'schema_version':1,'created_utc':datetime.now(timezone.utc).isoformat(),
        'branch':git(root,'branch','--show-current'),'source_commit':commit,
        'build_source_commit':build_commit,'fresh_clone_without_library':True,'build_inputs_identical_to_final_commit':True,
        'unity_version':'6000.4.7f1','platform':'Windows x86_64',
        'build_identity':'PtMeOH-FinalSubmission-Windows-x64-'+commit[:12],
        'build_result':'Succeeded','build_errors':int(match[1]),'build_warnings':int(match[2]),
        'build_log_sha256':digest(pathlib.Path(args.build_log)),
        'runtime_status':'Automated callbacks/API/synthetic-input/soak passed; physical input and team visual sign-off remain',
        'academic_status':'Numerically verified educational model; not empirically calibrated',
        'team_signoff':'Institution, team attribution and original model/icon permissions remain team input',
        'git_input_trees':{name:git(root,'rev-parse',commit+':'+name) for name in ('Assets','Packages','ProjectSettings')},
        'sha256':files}
    (package/'release-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
    archive=package.with_suffix('.zip')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for p in sorted(package.rglob('*')):
            if p.is_file():z.write(p,package.name+'/'+p.relative_to(package).as_posix())
    checksum=digest(archive)
    archive.with_suffix('.zip.sha256').write_text(checksum+'  '+archive.name+'\n',encoding='ascii')
    print(json.dumps({'source_commit':commit,'package':str(package),'archive':str(archive),'sha256':checksum,'files':len(files)},indent=2))
if __name__=='__main__':main()
