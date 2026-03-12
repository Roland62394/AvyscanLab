# RemoveDirt.avsi — compatible AviSynth+/AviSynth 2.6
# Dépendances: RemoveDirt.dll (pfmod), RgTools.dll
# Optionnel pour la variante MC: MVTools2.dll

function RemoveDirt(clip c, int "limit", bool "grey", int "dist")
{
    grey  = default(grey, false)
    limit = default(limit, 20)
    dist  = default(dist, 3)    # rayon de recherche spatial — augmenter pour taches plus grandes

    cl  = c.Clense(grey=grey, cache=4)   # RgTools
    # Mode 11 = flou gaussien spatial, neutre blanc ET noir
    # (Mode 2 = 2e minimum → biaisé vers sombre → inefficace pour taches noires)
    alt = c.RemoveGrain(11)              # RgTools

    # Appel "robuste" pour pfmod (évite les décalages de paramètres)
    return RestoreMotionBlocks( \
        cl, c, \
        alternative=alt, \
        pthreshold=8, \
        cthreshold=10, \
        gmthreshold=40, \
        dist=dist, \
        dmode=2, \
        debug=false, \
        noise=limit, \
        noisy=12, \
        grey=grey \
    )
}

function RemoveDirtMC(clip c, int "limit", bool "_grey", int "dist")
{
    _grey = default(_grey, false)
    limit = default(limit, 30)
    dist  = default(dist, 3)

    # Si MVTools2 n'est pas dispo, on retombe sur la version non-MC
    if (!FunctionExists("MSuper")) {
        return RemoveDirt(c, limit, _grey)
    }

    # MVTools2 (API "M*" classique)
    # blksize=8 : blocs plus robustes (blksize=4 perturbé par la tache elle-même)
    # pel=2 : précision sub-pixel suffisante, plus stable que pel=4
    i  = MSuper(c, pel=2, sharp=2)
    bv = MAnalyse(i, isb=false, blksize=8, delta=1, truemotion=true, chroma=false)
    fv = MAnalyse(i, isb=true,  blksize=8, delta=1, truemotion=true, chroma=false)
    bw = MFlow(c, i, bv)
    fw = MFlow(c, i, fv)

    clp = Interleave(bw, c, fw)
    clp = RemoveDirt(clp, limit, _grey, dist)
    return clp.SelectEvery(3,1)
}


