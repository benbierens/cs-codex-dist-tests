dotnet run \
    --kube-config=/opt/kubeconfig.yaml \
    --kube-namespace=codex-continuous-tests \
    --nodes=5 \
    --validators=3 \
    --storage-quota=2048 \
    --storage-sell=1024 \
    --min-price=1024 \
    --max-collateral=1024 \
    --max-duration=3600000 \
    --block-ttl=120

